using System.Text.Json;

namespace Pathdle.Generator.Corpus;

internal sealed class MediaWikiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate;
    private int _cooldownMs;

    public MediaWikiClient(int maxConcurrent = 1)
    {
        _gate = new SemaphoreSlim(maxConcurrent);
        var handler = new HttpClientHandler
        {
            AutomaticDecompression =
                System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        };
        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        // Wikimedia requires a descriptive UA with contact; keep requests polite (1 at a time).
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "PathdleGenerator/0.2 (https://github.com/pathdle; corpus builder; pathdle-dev@example.com)");
    }

    public async Task<Dictionary<string, List<string>>> GetOutboundLinksBatchAsync(
        IReadOnlyList<string> titles,
        CancellationToken ct,
        int maxPagesPerTitle = 5,
        IReadOnlySet<string>? onlyKeepIds = null)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        // Per-title isolation: one failure must not discard siblings' results.
        var tasks = titles.Select(async title =>
        {
            try
            {
                var links = await GetMainNamespaceLinksAsync(title, maxPagesPerTitle, ct, onlyKeepIds);
                lock (result)
                {
                    result[title] = links;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  warn: links '{title}': {ex.Message}");
                lock (result)
                {
                    result[title] = [];
                }
            }
        });

        await Task.WhenAll(tasks);
        return result;
    }

    /// <summary>
    /// Fetch main-namespace outbound links. When <paramref name="onlyKeepIds"/> is set,
    /// only those targets are retained. Pagination stops when MediaWiki is exhausted,
    /// the page cap is hit, or link titles have advanced past every remaining keep id
    /// (links are alphabetical).
    /// Pass <paramref name="maxPages"/> = 0 for unlimited pagination.
    /// </summary>
    public async Task<List<string>> GetMainNamespaceLinksAsync(
        string titleId,
        int maxPages,
        CancellationToken ct,
        IReadOnlySet<string>? onlyKeepIds = null)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_cooldownMs > 0)
            {
                await Task.Delay(_cooldownMs, ct);
            }

            var found = new HashSet<string>(StringComparer.Ordinal);
            string? plContinue = null;
            var pageTitle = TitleFromId(titleId);
            var page = 0;
            var consecutiveFailures = 0;

            string? maxKeepId = null;
            if (onlyKeepIds is { Count: > 0 })
            {
                maxKeepId = onlyKeepIds
                    .Where(id => !string.Equals(id, titleId, StringComparison.Ordinal))
                    .OrderByDescending(id => id, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
            }

            while (maxPages <= 0 || page < maxPages)
            {
                var url =
                    "https://en.wikipedia.org/w/api.php?action=query&format=json&formatversion=2"
                    + "&maxlag=5&prop=links&plnamespace=0&pllimit=500"
                    + "&titles=" + Uri.EscapeDataString(pageTitle);
                if (plContinue is not null)
                    url += "&plcontinue=" + Uri.EscapeDataString(plContinue);

                var (doc, rateLimited) = await GetJsonAsync(url, ct);
                if (doc is null)
                {
                    consecutiveFailures++;
                    if (rateLimited)
                    {
                        _cooldownMs = Math.Min(8_000, Math.Max(1_000, _cooldownMs * 2 + 500));
                    }

                    if (consecutiveFailures >= 4)
                    {
                        throw new InvalidOperationException(
                            $"MediaWiki link fetch failed repeatedly for '{titleId}' at page {page + 1}.");
                    }

                    await Task.Delay(300 * consecutiveFailures + _cooldownMs, ct);
                    continue;
                }

                using (doc)
                {
                    consecutiveFailures = 0;
                    if (_cooldownMs > 0) _cooldownMs = Math.Max(0, _cooldownMs - 200);
                    page++;

                    if (doc.RootElement.TryGetProperty("error", out var err))
                    {
                        var code = err.TryGetProperty("code", out var c) ? c.GetString() : "unknown";
                        var info = err.TryGetProperty("info", out var i) ? i.GetString() : "";
                        throw new InvalidOperationException($"MediaWiki error for '{titleId}': {code} {info}");
                    }

                    string? lastRawId = null;
                    if (doc.RootElement.TryGetProperty("query", out var query)
                        && query.TryGetProperty("pages", out var pages))
                    {
                        foreach (var p in pages.EnumerateArray())
                        {
                            if (p.TryGetProperty("missing", out _)) continue;
                            if (!p.TryGetProperty("links", out var links)) continue;
                            foreach (var link in links.EnumerateArray())
                            {
                                if (!link.TryGetProperty("title", out var t)) continue;
                                var id = NormalizeId(t.GetString());
                                if (id is null) continue;
                                lastRawId = id;
                                if (onlyKeepIds is not null && !onlyKeepIds.Contains(id)) continue;
                                found.Add(id);
                            }
                        }
                    }

                    plContinue = null;
                    if (doc.RootElement.TryGetProperty("continue", out var cont)
                        && cont.TryGetProperty("plcontinue", out var plc))
                    {
                        plContinue = plc.GetString();
                    }

                    if (plContinue is null) break;

                    if (maxKeepId is not null
                        && lastRawId is not null
                        && string.Compare(lastRawId, maxKeepId, StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        break;
                    }
                }

                await Task.Delay(75 + _cooldownMs / 4, ct);
            }

            return found.ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(JsonDocument? Doc, bool RateLimited)> GetJsonAsync(string url, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                var code = (int)response.StatusCode;
                if (code == 429 || code == 403 || code >= 500)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * (attempt + 1)), ct);
                    if (attempt == 3) return (null, code is 429 or 403);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"  warn: HTTP {code} for MediaWiki query");
                    return (null, false);
                }

                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                return (doc, false);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                await Task.Delay(300 * (attempt + 1), ct);
            }
            catch (HttpRequestException)
            {
                await Task.Delay(300 * (attempt + 1), ct);
            }
            catch (JsonException)
            {
                await Task.Delay(200 * (attempt + 1), ct);
            }
        }

        return (null, false);
    }

    public static string? NormalizeId(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var t = title.Trim().Replace(' ', '_');
        if (t.StartsWith("File:", StringComparison.OrdinalIgnoreCase)) return null;
        if (t.StartsWith("Category:", StringComparison.OrdinalIgnoreCase)) return null;
        if (t.StartsWith("Wikipedia:", StringComparison.OrdinalIgnoreCase)) return null;
        if (t.StartsWith("Help:", StringComparison.OrdinalIgnoreCase)) return null;
        if (t.StartsWith("Template:", StringComparison.OrdinalIgnoreCase)) return null;
        if (t.StartsWith("Portal:", StringComparison.OrdinalIgnoreCase)) return null;
        if (t.StartsWith("Draft:", StringComparison.OrdinalIgnoreCase)) return null;
        if (t.Contains('#')) t = t.Split('#')[0];
        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    public static string TitleFromId(string id) => id.Replace('_', ' ');

    public void Dispose()
    {
        _gate.Dispose();
        _http.Dispose();
    }
}
