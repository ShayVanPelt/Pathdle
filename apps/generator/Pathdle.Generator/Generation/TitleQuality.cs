using System.Text.RegularExpressions;

namespace Pathdle.Generator.Generation;

/// <summary>
/// Playable display names — reject missing English labels and near-duplicate board titles.
/// </summary>
internal static partial class TitleQuality
{
    [GeneratedRegex(@"^Q\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex QidOnly();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlnum();

    public static bool HasPlayableTitle(string? title, string entityId)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var t = title.Trim();
        if (t.Length < 2) return false;
        if (string.Equals(t, entityId, StringComparison.OrdinalIgnoreCase)) return false;
        if (QidOnly().IsMatch(t)) return false;
        return t.Any(char.IsLetter);
    }

    public static string Normalize(string title)
    {
        var lower = title.Trim().ToLowerInvariant();
        var paren = lower.IndexOf('(');
        if (paren > 0) lower = lower[..paren].Trim();
        return NonAlnum().Replace(lower, " ").Trim();
    }

    /// <summary>Series / franchise stem used to limit near-duplicates on one board.</summary>
    public static string FamilyKey(string title)
    {
        var raw = title.Trim().ToLowerInvariant();
        var c = raw.IndexOf(':');
        if (c > 3)
        {
            return NonAlnum().Replace(raw[..c], " ").Trim();
        }

        var n = Normalize(title);
        var parts = n.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2) return parts[0] + " " + parts[1];
        return n;
    }

    public static bool ConflictsWithBoard(
        string candidateTitle,
        IEnumerable<string> existingTitles,
        int maxPerFamily = 2)
    {
        var norm = Normalize(candidateTitle);
        if (string.IsNullOrEmpty(norm)) return true;

        var family = FamilyKey(candidateTitle);
        var familyCount = 0;
        foreach (var existing in existingTitles)
        {
            var en = Normalize(existing);
            if (en == norm) return true;
            if (en.Contains(norm, StringComparison.Ordinal) || norm.Contains(en, StringComparison.Ordinal))
            {
                if (Math.Min(en.Length, norm.Length) >= 8) return true;
            }

            if (FamilyKey(existing) == family) familyCount++;
        }

        return familyCount >= maxPerFamily;
    }
}
