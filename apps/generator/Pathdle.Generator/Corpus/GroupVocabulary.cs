namespace Pathdle.Generator.Corpus;

/// <summary>
/// Controlled Wikidata property vocabulary + hub denylist for unique groupings.
/// </summary>
internal static class GroupVocabulary
{
    /// <summary>Properties that create playable group memberships.</summary>
    public static readonly IReadOnlyList<PropertySpec> AllowedProperties =
    [
        new("P136", "genre"),
        new("P106", "occupation"),
        new("P452", "industry"),
        new("P641", "sport"),
        new("P178", "developer"),
        new("P123", "publisher"),
        new("P57", "director"),
        new("P50", "author"),
        new("P176", "manufacturer"),
        new("P179", "series"),
        new("P101", "field_of_work"),
        new("P495", "country_of_origin"),
        new("P449", "original_broadcaster"),
        new("P400", "platform"),
        new("P135", "movement"),
        new("P279", "subclass_of"),
    ];

    /// <summary>P31 instance-of classes allowed as entity seeds / typed buckets.</summary>
    public static readonly HashSet<string> AllowedInstanceClasses = new(StringComparer.Ordinal)
    {
        "Q5", // human — only kept when other groups exist; still denylist as a group value
        "Q11424", // film
        "Q5398426", // TV series
        "Q7889", // video game
        "Q4830453", // business / enterprise
        "Q783794", // company
        "Q43229", // organization
        "Q571", // book
        "Q482994", // album
        "Q7366", // song
        "Q515", // city
        "Q6256", // country
        "Q16521", // taxon
        "Q2095", // food
        "Q11173", // chemical compound
        "Q22698", // fruit? (use Q3314483 botanical fruit etc. — keep broad food)
        "Q3305213", // painting
        "Q215380", // musical group
    };

    /// <summary>Group value QIDs that must never create edges (mega-hubs).</summary>
    public static readonly HashSet<string> DeniedValueIds = new(StringComparer.Ordinal)
    {
        "Q30", // United States
        "Q145", // United Kingdom
        "Q142", // France
        "Q183", // Germany
        "Q17", // Japan
        "Q148", // China
        "Q5", // human
        "Q43229", // organization
        "Q4830453", // business
        "Q783794", // company
        "Q186165", // company (alt)
        "Q1860", // English
        "Q6581097", // male
        "Q6581072", // female
        "Q15978631", // Homo sapiens
        "Q15401930", // product
        "Q2424752", // product
    };

    public const int MinGroupSize = 4;
    public const int MaxGroupSize = 400;

    /// <summary>Drop long-tail entities with no English Wikipedia page.</summary>
    public const int MinSitelinks = 8;

    public static string GroupId(string property, string valueId) => $"{property}:{valueId}";

    public static double Rarity(int memberCount, int totalEntities)
    {
        if (totalEntities <= 0 || memberCount <= 0) return 0;
        var frequency = (double)memberCount / totalEntities;
        return Math.Sqrt(1.0 / frequency);
    }

    internal sealed record PropertySpec(string Id, string Slug);
}
