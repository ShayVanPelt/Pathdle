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
                "CREATE CONSTRAINT article_id IF NOT EXISTS FOR (a:Article) REQUIRE a.id IS UNIQUE");
            await tx.RunAsync(
                "CREATE CONSTRAINT corpus_meta_version IF NOT EXISTS FOR (c:CorpusMeta) REQUIRE c.version IS UNIQUE");
        });
    }

    public async Task ClearCorpusAsync(CancellationToken ct)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync("MATCH (a:Article) DETACH DELETE a");
            await tx.RunAsync("MATCH (c:CorpusMeta) DELETE c");
        });
    }

    public async Task UpsertArticlesAndEdgesAsync(
        IReadOnlyDictionary<string, string> idToTitle,
        IReadOnlyCollection<(string From, string To)> edges,
        CancellationToken ct)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));

        foreach (var chunk in idToTitle.Chunk(200))
        {
            var rows = chunk.Select(kv => new { id = kv.Key, title = kv.Value }).ToList();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                    UNWIND $rows AS row
                    MERGE (a:Article {id: row.id})
                    SET a.title = row.title
                    """,
                    new { rows });
            });
        }

        foreach (var chunk in edges.Chunk(400))
        {
            var rows = chunk.Select(e => new { from = e.From, to = e.To }).ToList();
            await session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync(
                    """
                    UNWIND $rows AS row
                    MATCH (a:Article {id: row.from})
                    MATCH (b:Article {id: row.to})
                    MERGE (a)-[:LINKS_TO]->(b)
                    """,
                    new { rows });
            });
        }

        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                MATCH (a:Article)
                OPTIONAL MATCH (a)-[o:LINKS_TO]->()
                WITH a, count(o) AS dout
                OPTIONAL MATCH ()-[i:LINKS_TO]->(a)
                WITH a, dout, count(i) AS din
                SET a.degree_out = dout, a.degree_in = din,
                    a.popularity = din + dout
                """);
        });
    }

    public async Task WriteCorpusMetaAsync(
        string version,
        int articleCount,
        int edgeCount,
        CancellationToken ct)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(database));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(
                """
                MERGE (c:CorpusMeta {version: $version})
                SET c.article_count = $articleCount,
                    c.edge_count = $edgeCount,
                    c.built_at = datetime()
                """,
                new { version, articleCount, edgeCount });
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
                RETURN c.version AS version, c.article_count AS articles, c.edge_count AS edges
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
                MATCH (a:Article)
                RETURN a.id AS id, a.title AS title,
                       coalesce(a.degree_out, 0) AS degreeOut,
                       coalesce(a.degree_in, 0) AS degreeIn,
                       coalesce(a.popularity, 0) AS popularity
                """);
            await foreach (var record in cursor)
            {
                var id = record["id"].As<string>();
                articles[id] = new CorpusArticle(
                    id,
                    record["title"].As<string>(),
                    record["degreeOut"].As<int>(),
                    record["degreeIn"].As<int>(),
                    record["popularity"].As<int>());
            }

            var outs = articles.Keys.ToDictionary(
                k => k,
                _ => new List<string>(),
                StringComparer.Ordinal);

            var edgeCursor = await tx.RunAsync(
                """
                MATCH (a:Article)-[:LINKS_TO]->(b:Article)
                RETURN a.id AS from, b.id AS to
                """);
            var edgeCount = 0;
            await foreach (var record in edgeCursor)
            {
                var from = record["from"].As<string>();
                var to = record["to"].As<string>();
                if (outs.TryGetValue(from, out var list))
                {
                    list.Add(to);
                    edgeCount++;
                }
            }

            return new CorpusGraph(articles, outs, edgeCount);
        });
    }

    public ValueTask DisposeAsync() => driver.DisposeAsync();
}

internal sealed record CorpusArticle(
    string Id,
    string Title,
    int DegreeOut,
    int DegreeIn,
    int Popularity);

internal sealed class CorpusGraph(
    IReadOnlyDictionary<string, CorpusArticle> articles,
    IReadOnlyDictionary<string, List<string>> outbound,
    int edgeCount)
{
    public IReadOnlyDictionary<string, CorpusArticle> Articles { get; } = articles;
    public IReadOnlyDictionary<string, List<string>> Outbound { get; } = outbound;
    public int EdgeCount { get; } = edgeCount;

    public IReadOnlyList<string> Neighbors(string id) =>
        Outbound.TryGetValue(id, out var list) ? list : Array.Empty<string>();
}
