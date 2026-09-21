using Neo4j.Driver;

namespace Pathdle.Generator.Corpus;

internal sealed class Neo4jCorpusStore(IDriver driver, string database) : IAsyncDisposable
{
    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                "CREATE CONSTRAINT entity_id IF NOT EXISTS FOR (e:Entity) REQUIRE e.id IS UNIQUE");
            await tx.RunAsync(
                "CREATE CONSTRAINT group_id IF NOT EXISTS FOR (g:Group) REQUIRE g.id IS UNIQUE");
            await tx.RunAsync(
                "CREATE CONSTRAINT corpus_meta_version IF NOT EXISTS FOR (c:CorpusMeta) REQUIRE c.version IS UNIQUE");
        });
    }

    public async Task ClearCorpusAsync(CancellationToken ct)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync("MATCH (e:Entity) DETACH DELETE e");
            await tx.RunAsync("MATCH (g:Group) DETACH DELETE g");
            await tx.RunAsync("MATCH (a:Article) DETACH DELETE a");
            await tx.RunAsync("MATCH (c:CorpusMeta) DELETE c");
        });
    }

    public async Task UpsertCorpusAsync(
        IReadOnlyList<CorpusEntityWrite> entities,
        IReadOnlyList<CorpusGroupWrite> groups,
        IReadOnlyList<(string EntityId, string GroupId)> memberships,
        IReadOnlyList<CorpusShareEdgeWrite> shareEdges,
        CancellationToken ct)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));

        foreach (var chunk in groups.Chunk(200))
        {
            var rows = chunk.Select(g => new
            {
                id = g.Id,
                label = g.Label,
                property = g.Property,
                valueId = g.ValueId,
                frequency = g.Frequency,
                rarity = g.Rarity,
                memberCount = g.MemberCount
            }).ToList();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                    UNWIND $rows AS row
                    MERGE (g:Group {id: row.id})
                    SET g.label = row.label,
                        g.property = row.property,
                        g.valueId = row.valueId,
                        g.frequency = row.frequency,
                        g.rarity = row.rarity,
                        g.memberCount = row.memberCount
                    """,
                    new { rows });
            });
        }

        foreach (var chunk in entities.Chunk(200))
        {
            var rows = chunk.Select(e => new
            {
                id = e.Id,
                title = e.Title,
                popularity = e.Popularity,
                description = e.Description ?? "",
                isSeed = e.IsSeed
            }).ToList();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                    UNWIND $rows AS row
                    MERGE (e:Entity {id: row.id})
                    SET e.title = row.title,
                        e.popularity = row.popularity,
                        e.description = row.description,
                        e.isSeed = row.isSeed
                    """,
                    new { rows });
            });
        }

        foreach (var chunk in memberships.Chunk(400))
        {
            var rows = chunk.Select(m => new { entityId = m.EntityId, groupId = m.GroupId }).ToList();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                    UNWIND $rows AS row
                    MATCH (e:Entity {id: row.entityId})
                    MATCH (g:Group {id: row.groupId})
                    MERGE (e)-[:IN_GROUP]->(g)
                    """,
                    new { rows });
            });
        }

        // Clear prior share edges then rewrite (idempotent rebuild).
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync("MATCH (:Entity)-[r:SHARES_GROUP]->(:Entity) DELETE r");
        });

        foreach (var chunk in shareEdges.Chunk(400))
        {
            var rows = chunk.Select(e => new
            {
                a = e.A,
                b = e.B,
                groupId = e.GroupId,
                groupLabel = e.GroupLabel,
                rarity = e.Rarity
            }).ToList();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                    UNWIND $rows AS row
                    MATCH (a:Entity {id: row.a})
                    MATCH (b:Entity {id: row.b})
                    MERGE (a)-[r:SHARES_GROUP {groupId: row.groupId}]->(b)
                    SET r.groupLabel = row.groupLabel, r.rarity = row.rarity
                    """,
                    new { rows });
            });
        }
    }

    public async Task WriteCorpusMetaAsync(
        string version,
        int entityCount,
        int groupCount,
        int edgeCount,
        CancellationToken ct)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                MERGE (c:CorpusMeta {version: $version})
                SET c.entity_count = $entityCount,
                    c.group_count = $groupCount,
                    c.article_count = $entityCount,
                    c.edge_count = $edgeCount,
                    c.built_at = datetime()
                """,
                new { version, entityCount, groupCount, edgeCount });
        });
    }

    public async Task<(string Version, int Articles, int Edges)?> GetLatestMetaAsync(CancellationToken ct)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(
                """
                MATCH (c:CorpusMeta)
                RETURN c.version AS version,
                       coalesce(c.entity_count, c.article_count, 0) AS articles,
                       coalesce(c.edge_count, 0) AS edges
                ORDER BY c.built_at DESC
                LIMIT 1
                """);
            if (!await cursor.FetchAsync())
            {
                return ((string Version, int Articles, int Edges)?)null;
            }

            return (
                cursor.Current["version"].As<string>(),
                cursor.Current["articles"].As<int>(),
                cursor.Current["edges"].As<int>());
        });
    }

    public async Task<CorpusGraph> LoadGraphAsync(CancellationToken ct)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        return await session.ExecuteReadAsync(async tx =>
        {
            var articles = new Dictionary<string, CorpusArticle>(StringComparer.Ordinal);
            var cursor = await tx.RunAsync(
                """
                MATCH (e:Entity)
                RETURN e.id AS id, e.title AS title,
                       coalesce(e.popularity, 0) AS popularity,
                       coalesce(e.description, '') AS description,
                       coalesce(e.isSeed, false) AS isSeed
                """);
            await foreach (var record in cursor)
            {
                var id = record["id"].As<string>();
                var popularity = record["popularity"].As<int>();
                var description = record["description"].As<string>();
                var isSeed = record["isSeed"].As<bool>();
                articles[id] = new CorpusArticle(
                    id,
                    record["title"].As<string>(),
                    string.IsNullOrWhiteSpace(description) ? null : description,
                    popularity,
                    popularity,
                    popularity,
                    isSeed);
            }

            var neighbors = articles.Keys.ToDictionary(
                k => k,
                _ => new List<CorpusEdge>(),
                StringComparer.Ordinal);

            var edgeCursor = await tx.RunAsync(
                """
                MATCH (a:Entity)-[r:SHARES_GROUP]->(b:Entity)
                RETURN a.id AS from, b.id AS to,
                       r.groupId AS groupId, r.groupLabel AS groupLabel,
                       coalesce(r.rarity, 1.0) AS rarity
                """);
            var edgeCount = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            await foreach (var record in edgeCursor)
            {
                var from = record["from"].As<string>();
                var to = record["to"].As<string>();
                var groupId = record["groupId"].As<string>();
                var groupLabel = record["groupLabel"].As<string>();
                var rarity = record["rarity"].As<double>();
                var key = CorpusGraph.UndirectedKey(from, to);
                if (!seen.Add(key)) continue;

                var edge = new CorpusEdge(from, to, groupId, groupLabel, rarity);
                if (neighbors.TryGetValue(from, out var fl)) fl.Add(edge);
                if (neighbors.TryGetValue(to, out var tl)) tl.Add(edge with { From = to, To = from });
                edgeCount++;
            }

            return new CorpusGraph(articles, neighbors, edgeCount);
        });
    }

    public ValueTask DisposeAsync() => driver.DisposeAsync();
}

internal sealed record CorpusEntityWrite(
    string Id,
    string Title,
    int Popularity,
    string? Description = null,
    bool IsSeed = false);
internal sealed record CorpusGroupWrite(
    string Id,
    string Label,
    string Property,
    string ValueId,
    double Frequency,
    double Rarity,
    int MemberCount);
internal sealed record CorpusShareEdgeWrite(
    string A,
    string B,
    string GroupId,
    string GroupLabel,
    double Rarity);

internal sealed record CorpusArticle(
    string Id,
    string Title,
    string? Description,
    int DegreeOut,
    int DegreeIn,
    int Popularity,
    bool IsSeed = false);

internal sealed record CorpusEdge(
    string From,
    string To,
    string GroupId,
    string GroupLabel,
    double Rarity);

internal sealed class CorpusGraph(
    IReadOnlyDictionary<string, CorpusArticle> articles,
    IReadOnlyDictionary<string, List<CorpusEdge>> adjacency,
    int edgeCount)
{
    public IReadOnlyDictionary<string, CorpusArticle> Articles { get; } = articles;
    public IReadOnlyDictionary<string, List<CorpusEdge>> Adjacency { get; } = adjacency;
    public int EdgeCount { get; } = edgeCount;

    public IReadOnlyList<CorpusEdge> EdgesFrom(string id) =>
        Adjacency.TryGetValue(id, out var list) ? list : Array.Empty<CorpusEdge>();

    public IReadOnlyList<string> Neighbors(string id) =>
        EdgesFrom(id).Select(e => e.To).Distinct(StringComparer.Ordinal).ToList();

    public CorpusEdge? FindEdge(string a, string b)
    {
        foreach (var e in EdgesFrom(a))
        {
            if (string.Equals(e.To, b, StringComparison.Ordinal)) return e;
        }

        return null;
    }

    public static string UndirectedKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
}
