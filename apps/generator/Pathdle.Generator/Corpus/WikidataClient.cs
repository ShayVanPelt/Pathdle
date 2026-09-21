using System.Net.Http.Headers;
using System.Text.Json;

namespace Pathdle.Generator.Corpus;

/// <summary>
/// Minimal Wikidata SPARQL + wbgetentities client for corpus ingest.
/// Retries and validates JSON so HTML error pages from rate limits don't crash ingest.
/// </summary>
internal sealed class WikidataClient : IDisposable
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public WikidataClient()
    {
        // Wikimedia asks for a descriptive UA with contact.
        _http.DefaultRequestHeaders.UserAgent.Clear();
        _http.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "PathdleGenerator/1.0 (https://github.com/pathdle; local corpus ingest)");
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IReadOnlyList<string>> SearchEntityIdsByInstanceOfAsync(
        string classQid,
        int limit,
        CancellationToken ct)
    {
        var limitClamped = Math.Clamp(limit, 10, 2000);
        var sparql =
            "SELECT ?item WHERE {\n"
            + $"  ?item wdt:P31 wd:{classQid} .\n"
            + "}\n"
            + $"LIMIT {limitClamped}";

        var rows = await RunSparqlAsync(sparql, ct);
        var ids = new List<string>();
        foreach (var row in rows)
        {
            if (!row.TryGetValue("item", out var uri) || string.IsNullOrEmpty(uri)) continue;
            var qid = uri.Split('/').LastOrDefault();
            if (qid is not null && qid.StartsWith('Q'))
            {
                ids.Add(qid);
            }
        }

        return ids;
    }

    public async Task<IReadOnlyDictionary<string, WikidataEntity>> GetEntitiesAsync(
        IReadOnlyList<string> ids,
        CancellationToken ct)
    {
        var result = new Dictionary<string, WikidataEntity>(StringComparer.Ordinal);
        // Claims payloads are large — keep chunks small to avoid HTML error pages / timeouts.
        const int chunkSize = 20;
        var distinct = ids.Distinct(StringComparer.Ordinal).ToList();
        var chunks = distinct.Chunk(chunkSize).ToList();
        var index = 0;
        foreach (var chunk in chunks)
        {
            index++;
            if (index == 1 || index % 10 == 0 || index == chunks.Count)
            {
                Console.WriteLine($"  wbgetentities claims {index}/{chunks.Count} ({result.Count} loaded)…");
            }

            var url =
                "https://www.wikidata.org/w/api.php?action=wbgetentities"
                + $"&ids={string.Join('|', chunk)}"
                + "&props=labels|descriptions|claims|sitelinks&languages=en&format=json";

            using var doc = await GetJsonDocumentAsync(url, ct);
            if (!doc.RootElement.TryGetProperty("entities", out var entities))
            {
                continue;
            }

            foreach (var prop in entities.EnumerateObject())
            {
                var id = prop.Name;
                var el = prop.Value;
                if (el.TryGetProperty("missing", out _)) continue;

                var title = id;
                if (el.TryGetProperty("labels", out var labels)
                    && labels.TryGetProperty("en", out var en)
                    && en.TryGetProperty("value", out var titleEl))
                {
                    title = titleEl.GetString() ?? id;
                }

                string? description = null;
                if (el.TryGetProperty("descriptions", out var descriptions)
                    && descriptions.TryGetProperty("en", out var den)
                    && den.TryGetProperty("value", out var descEl))
                {
                    description = descEl.GetString();
                }

                var hasEnWiki = false;
                var sitelinks = 0;
                if (el.TryGetProperty("sitelinks", out var links))
                {
                    foreach (var link in links.EnumerateObject())
                    {
                        sitelinks++;
                        if (link.Name == "enwiki") hasEnWiki = true;
                    }
                }

                var memberships = new List<(string Property, string ValueId)>();
                if (el.TryGetProperty("claims", out var claims))
                {
                    foreach (var allowed in GroupVocabulary.AllowedProperties)
                    {
                        if (!claims.TryGetProperty(allowed.Id, out var claimArr)) continue;
                        foreach (var claim in claimArr.EnumerateArray())
                        {
                            if (!TryReadEntityValue(claim, out var valueId)) continue;
                            memberships.Add((allowed.Id, valueId));
                        }
                    }

                    if (claims.TryGetProperty("P31", out var p31))
                    {
                        foreach (var claim in p31.EnumerateArray())
                        {
                            if (!TryReadEntityValue(claim, out var valueId)) continue;
                            if (GroupVocabulary.AllowedInstanceClasses.Contains(valueId)
                                && !GroupVocabulary.DeniedValueIds.Contains(valueId))
                            {
                                memberships.Add(("P31", valueId));
                            }
                        }
                    }
                }

                result[id] = new WikidataEntity(
                    id, title, description, sitelinks, hasEnWiki, memberships);
            }

            await Task.Delay(150, ct);
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetLabelsAsync(
        IReadOnlyList<string> ids,
        CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var chunks = ids.Distinct(StringComparer.Ordinal).Chunk(40).ToList();
        var index = 0;
        foreach (var chunk in chunks)
        {
            index++;
            if (index == 1 || index % 15 == 0 || index == chunks.Count)
            {
                Console.WriteLine($"  wbgetentities labels {index}/{chunks.Count}…");
            }

            var url =
                "https://www.wikidata.org/w/api.php?action=wbgetentities"
                + $"&ids={string.Join('|', chunk)}"
                + "&props=labels&languages=en&format=json";

            using var doc = await GetJsonDocumentAsync(url, ct);
            if (!doc.RootElement.TryGetProperty("entities", out var entities)) continue;
            foreach (var prop in entities.EnumerateObject())
            {
                var id = prop.Name;
                var title = id;
                if (prop.Value.TryGetProperty("labels", out var labels)
                    && labels.TryGetProperty("en", out var en)
                    && en.TryGetProperty("value", out var v))
                {
                    title = v.GetString() ?? id;
                }

                map[id] = title;
            }

            await Task.Delay(100, ct);
        }

        return map;
    }

    private async Task<List<Dictionary<string, string>>> RunSparqlAsync(
        string sparql,
        CancellationToken ct)
    {
        using var response = await SendWithRetryAsync(
            () =>
            {
                // New content each attempt — FormUrlEncodedContent is single-use.
                var content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("query", sparql),
                    new KeyValuePair<string, string>("format", "json"),
                ]);
                return _http.PostAsync("https://query.wikidata.org/sparql", content, ct);
            },
            ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureJsonBody(body, "SPARQL");

        using var doc = JsonDocument.Parse(body);
        var rows = new List<Dictionary<string, string>>();
        if (!doc.RootElement.TryGetProperty("results", out var results)
            || !results.TryGetProperty("bindings", out var bindings))
        {
            return rows;
        }

        foreach (var binding in bindings.EnumerateArray())
        {
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in binding.EnumerateObject())
            {
                if (p.Value.TryGetProperty("value", out var valueEl))
                {
                    row[p.Name] = valueEl.GetString() ?? "";
                }
            }

            if (row.Count > 0)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private async Task<JsonDocument> GetJsonDocumentAsync(string url, CancellationToken ct)
    {
        using var response = await SendWithRetryAsync(() => _http.GetAsync(url, ct), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureJsonBody(body, url);
        return JsonDocument.Parse(body);
    }

    private static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<Task<HttpResponseMessage>> send,
        CancellationToken ct)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var response = await send();
            if ((int)response.StatusCode is 429 or >= 500)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                Console.WriteLine(
                    $"  Wikidata HTTP {(int)response.StatusCode}, retry {attempt}/{maxAttempts} in {delay.TotalSeconds:0}s…");
                response.Dispose();
                await Task.Delay(delay, ct);
                continue;
            }

            response.EnsureSuccessStatusCode();
            return response;
        }

        throw new InvalidOperationException("Wikidata request failed after retries.");
    }

    private static void EnsureJsonBody(string body, string context)
    {
        var trimmed = body.AsSpan().TrimStart();
        if (trimmed.Length > 0 && trimmed[0] is '{' or '[')
        {
            return;
        }

        var preview = body.Length <= 160 ? body : body[..160];
        throw new InvalidOperationException(
            $"Wikidata returned non-JSON for {context}. "
            + "Often rate-limiting or a blocked request. Preview: "
            + preview.Replace('\n', ' ').Replace('\r', ' '));
    }

    private static bool TryReadEntityValue(JsonElement claim, out string valueId)
    {
        valueId = "";
        if (!claim.TryGetProperty("mainsnak", out var snak)) return false;
        if (!snak.TryGetProperty("datavalue", out var dv)) return false;
        if (!dv.TryGetProperty("value", out var value)) return false;
        if (!value.TryGetProperty("id", out var idEl)) return false;
        valueId = idEl.GetString() ?? "";
        return valueId.StartsWith('Q');
    }

    public void Dispose() => _http.Dispose();
}

internal sealed record WikidataEntity(
    string Id,
    string Title,
    string? Description,
    int SitelinkCount,
    bool HasEnWiki,
    IReadOnlyList<(string Property, string ValueId)> Memberships);
