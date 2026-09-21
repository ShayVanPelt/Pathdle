using Pathdle.Application.Models;

namespace Pathdle.Infrastructure.Seed;

/// <summary>
/// Hand-authored group-graph seed for local MVP (no Neo4j/Wikidata).
/// Optimal: Dell → Apple → Orange → Vitamin_C (3 connections via Technology / Fruit / Nutrient).
/// </summary>
public static class SeedPuzzleFactory
{
    public static readonly Guid PuzzleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static DailyPuzzle CreateForDate(DateOnly puzzleDate)
    {
        var nodes = new List<PuzzleNode>
        {
            N("Dell", "Dell", 0.10, 0.48, NodeKind.Start, "American computer company"),
            N("Apple", "Apple", 0.38, 0.42, description: "Fruit; also tech company"),
            N("Orange", "Orange", 0.62, 0.48, description: "Citrus fruit"),
            N("Vitamin_C", "Vitamin C", 0.90, 0.52, NodeKind.Target, "Essential nutrient"),
            N("Microsoft", "Microsoft", 0.22, 0.28, description: "American software company"),
            N("Google", "Google", 0.28, 0.18, description: "American technology company"),
            N("Sony", "Sony", 0.18, 0.62, description: "Japanese electronics company"),
            N("Banana", "Banana", 0.55, 0.28, description: "Tropical fruit"),
            N("Pear", "Pear", 0.70, 0.32, description: "Tree fruit"),
            N("Lemon", "Lemon", 0.72, 0.62, description: "Citrus fruit"),
            N("HP", "HP", 0.12, 0.32, description: "American computer company"),
            N("Intel", "Intel", 0.32, 0.55, description: "American semiconductor company"),
            N("iPhone", "iPhone", 0.42, 0.22, description: "Apple smartphone"),
            N("Macintosh", "Macintosh", 0.48, 0.58, description: "Apple personal computer"),
            N("California", "California", 0.35, 0.72, description: "U.S. state"),
            N("Cupertino", "Cupertino", 0.45, 0.78, description: "City in California"),
            N("Citrus", "Citrus", 0.68, 0.72, description: "Fruit genus"),
            N("Ascorbic_acid", "Ascorbic acid", 0.82, 0.38, description: "Chemical name for vitamin C"),
            N("Scurvy", "Scurvy", 0.88, 0.68, description: "Vitamin C deficiency disease"),
            N("Kiwi_fruit", "Kiwi fruit", 0.58, 0.18, description: "Fuzzy green fruit"),
            N("Strawberry", "Strawberry", 0.78, 0.22, description: "Red berry fruit"),
            N("Samsung", "Samsung", 0.20, 0.78, description: "South Korean conglomerate"),
            N("PlayStation", "PlayStation", 0.08, 0.72, description: "Sony game console"),
            N("Windows", "Windows", 0.30, 0.38, description: "Microsoft operating system"),
            N("Android", "Android", 0.38, 0.12, description: "Google mobile OS"),
            N("Steve_Jobs", "Steve Jobs", 0.50, 0.68, description: "Apple co-founder"),
            N("Tim_Cook", "Tim Cook", 0.52, 0.85, description: "Apple CEO"),
            N("Fruit_salad", "Fruit salad", 0.65, 0.55, description: "Dish of mixed fruit"),
            N("Orange_juice", "Orange juice", 0.75, 0.48, description: "Juice from oranges"),
            N("Broccoli", "Broccoli", 0.85, 0.28, description: "Green vegetable"),
            N("Pepper", "Bell pepper", 0.82, 0.58, description: "Vegetable"),
            N("Nintendo", "Nintendo", 0.15, 0.88, description: "Japanese video game company"),
        };

        var edges = new List<PuzzleEdge>
        {
            // Optimal spine
            G("Dell", "Apple", "industry:tech_company", "Technology company"),
            G("Apple", "Orange", "category:fruit", "Fruit"),
            G("Orange", "Vitamin_C", "nutrient:vitamin_c", "Contains vitamin C"),

            // Technology company cluster
            G("Dell", "Microsoft", "industry:tech_company", "Technology company"),
            G("Dell", "HP", "industry:tech_company", "Technology company"),
            G("Apple", "Microsoft", "industry:tech_company", "Technology company"),
            G("Apple", "Google", "industry:tech_company", "Technology company"),
            G("Apple", "Sony", "industry:tech_company", "Technology company"),
            G("Microsoft", "Google", "industry:tech_company", "Technology company"),
            G("Microsoft", "Sony", "industry:tech_company", "Technology company"),
            G("Google", "Samsung", "industry:tech_company", "Technology company"),
            G("Sony", "Samsung", "industry:tech_company", "Technology company"),
            G("HP", "Intel", "industry:tech_company", "Technology company"),
            G("Intel", "Microsoft", "industry:tech_company", "Technology company"),

            // Fruit cluster
            G("Apple", "Banana", "category:fruit", "Fruit"),
            G("Apple", "Pear", "category:fruit", "Fruit"),
            G("Orange", "Banana", "category:fruit", "Fruit"),
            G("Orange", "Pear", "category:fruit", "Fruit"),
            G("Orange", "Lemon", "category:fruit", "Fruit"),
            G("Banana", "Pear", "category:fruit", "Fruit"),
            G("Banana", "Kiwi_fruit", "category:fruit", "Fruit"),
            G("Pear", "Kiwi_fruit", "category:fruit", "Fruit"),
            G("Lemon", "Kiwi_fruit", "category:fruit", "Fruit"),
            G("Kiwi_fruit", "Strawberry", "category:fruit", "Fruit"),
            G("Strawberry", "Orange", "category:fruit", "Fruit"),

            // Vitamin C sources
            G("Lemon", "Vitamin_C", "nutrient:vitamin_c", "Contains vitamin C"),
            G("Kiwi_fruit", "Vitamin_C", "nutrient:vitamin_c", "Contains vitamin C"),
            G("Strawberry", "Vitamin_C", "nutrient:vitamin_c", "Contains vitamin C"),
            G("Broccoli", "Vitamin_C", "nutrient:vitamin_c", "Contains vitamin C"),
            G("Pepper", "Vitamin_C", "nutrient:vitamin_c", "Contains vitamin C"),
            G("Orange_juice", "Vitamin_C", "nutrient:vitamin_c", "Contains vitamin C"),

            // Citrus
            G("Orange", "Lemon", "botany:citrus", "Citrus fruit"),
            G("Orange", "Citrus", "botany:citrus", "Citrus fruit"),
            G("Lemon", "Citrus", "botany:citrus", "Citrus fruit"),

            // Apple products / people
            G("Apple", "iPhone", "maker:apple_inc", "Made by Apple"),
            G("Apple", "Macintosh", "maker:apple_inc", "Made by Apple"),
            G("iPhone", "Macintosh", "maker:apple_inc", "Made by Apple"),
            G("Apple", "Steve_Jobs", "org:apple_leadership", "Apple leadership"),
            G("Apple", "Tim_Cook", "org:apple_leadership", "Apple leadership"),
            G("Steve_Jobs", "Tim_Cook", "org:apple_leadership", "Apple leadership"),

            // California / Cupertino
            G("Apple", "Cupertino", "hq:cupertino", "Based in Cupertino"),
            G("Cupertino", "California", "place:california", "Located in California"),
            G("Google", "California", "place:california", "Located in California"),

            // Platforms / games traps
            G("Microsoft", "Windows", "platform:os_vendor", "Operating system vendor"),
            G("Google", "Android", "platform:os_vendor", "Operating system vendor"),
            G("Sony", "PlayStation", "franchise:sony_gaming", "Sony gaming"),
            G("Nintendo", "PlayStation", "category:game_console", "Game console maker"),
            G("Sony", "Nintendo", "category:game_console", "Game console maker"),
            G("Samsung", "Android", "platform:android_devices", "Android devices"),

            // Food prep
            G("Banana", "Fruit_salad", "dish:fruit_salad", "Fruit salad ingredient"),
            G("Orange", "Fruit_salad", "dish:fruit_salad", "Fruit salad ingredient"),
            G("Strawberry", "Fruit_salad", "dish:fruit_salad", "Fruit salad ingredient"),
            G("Orange", "Orange_juice", "product:orange_juice", "Orange juice"),
            G("Ascorbic_acid", "Vitamin_C", "chem:ascorbic", "Also known as"),
            G("Scurvy", "Vitamin_C", "medicine:scurvy", "Prevents scurvy"),
            G("Broccoli", "Pepper", "category:vegetable", "Vegetable"),
        };

        var optimalPath = new[] { "Dell", "Apple", "Orange", "Vitamin_C" };

        return new DailyPuzzle
        {
            Id = PuzzleId,
            PuzzleDate = puzzleDate,
            GraphVersion = $"{puzzleDate:yyyy-MM-dd}.group-seed",
            StartArticleId = "Dell",
            TargetArticleId = "Vitamin_C",
            Nodes = nodes,
            Edges = edges,
            OptimalPath = optimalPath,
            OptimalLength = optimalPath.Length - 1,
            GeneratorSeed = "seed-group-graph-v1",
            CorpusVersion = "hand-authored-groups-v1",
            Difficulty = new { note = "hand-authored group-graph seed", band = "medium" },
            PublishedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static PuzzleNode N(
        string id,
        string title,
        double x,
        double y,
        NodeKind kind = NodeKind.Normal,
        string? description = null) =>
        new(id, title, x, y, kind, description);

    private static PuzzleEdge G(string a, string b, string groupId, string groupLabel) =>
        new(a, b, groupId, groupLabel);
}
