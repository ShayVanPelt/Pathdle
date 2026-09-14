using System.Text.Json;
using Npgsql;
using Pathdle.Application.Abstractions;
using Pathdle.Application.Models;

namespace Pathdle.Infrastructure.Persistence.Postgres;

public sealed class PostgresPuzzleRepository(NpgsqlDataSource dataSource) : IPuzzleRepository
{
    public async Task<DailyPuzzle?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            select id, puzzle_date, graph_version, start_article_id, target_article_id,
                   nodes, edges, optimal_path, optimal_length, generator_seed, corpus_version,
                   published_at, created_at
            from daily_puzzles
            where puzzle_date = @date
            """,
            conn);
        cmd.Parameters.AddWithValue("date", date);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<DailyPuzzle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            select id, puzzle_date, graph_version, start_article_id, target_article_id,
                   nodes, edges, optimal_path, optimal_length, generator_seed, corpus_version,
                   published_at, created_at
            from daily_puzzles
            where id = @id
            """,
            conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    /// <summary>
    /// Inserts a puzzle only if that calendar date is empty (published puzzles are immutable).
    /// </summary>
    public async Task<bool> TryInsertAsync(DailyPuzzle puzzle, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            insert into daily_puzzles (
              id, puzzle_date, graph_version, start_article_id, target_article_id,
              nodes, edges, optimal_path, optimal_length, difficulty,
              generator_seed, corpus_version, published_at, created_at
            ) values (
              @id, @puzzle_date, @graph_version, @start_article_id, @target_article_id,
              @nodes::jsonb, @edges::jsonb, @optimal_path::jsonb, @optimal_length, @difficulty::jsonb,
              @generator_seed, @corpus_version, @published_at, @created_at
            )
            on conflict (puzzle_date) do nothing
            """,
            conn);

        var nodeRows = puzzle.Nodes
            .Select(n => new NodeRow(n.Id, n.Title, n.X, n.Y, PostgresJson.FormatKind(n.Kind)))
            .ToList();
        var edgeRows = puzzle.Edges.Select(e => new EdgeRow(e.From, e.To)).ToList();

        cmd.Parameters.AddWithValue("id", puzzle.Id);
        cmd.Parameters.AddWithValue("puzzle_date", puzzle.PuzzleDate);
        cmd.Parameters.AddWithValue("graph_version", puzzle.GraphVersion);
        cmd.Parameters.AddWithValue("start_article_id", puzzle.StartArticleId);
        cmd.Parameters.AddWithValue("target_article_id", puzzle.TargetArticleId);
        cmd.Parameters.AddWithValue("nodes", PostgresJson.Serialize(nodeRows));
        cmd.Parameters.AddWithValue("edges", PostgresJson.Serialize(edgeRows));
        cmd.Parameters.AddWithValue("optimal_path", PostgresJson.Serialize(puzzle.OptimalPath));
        cmd.Parameters.AddWithValue("optimal_length", puzzle.OptimalLength);
        cmd.Parameters.AddWithValue(
            "difficulty",
            PostgresJson.Serialize(puzzle.Difficulty ?? new { note = "unspecified" }));
        cmd.Parameters.AddWithValue("generator_seed", puzzle.GeneratorSeed);
        cmd.Parameters.AddWithValue("corpus_version", puzzle.CorpusVersion);
        cmd.Parameters.AddWithValue("published_at", puzzle.PublishedAt);
        cmd.Parameters.AddWithValue("created_at", puzzle.CreatedAt);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    private static DailyPuzzle Map(NpgsqlDataReader reader)
    {
        var nodesJson = reader.GetString(reader.GetOrdinal("nodes"));
        var edgesJson = reader.GetString(reader.GetOrdinal("edges"));
        var pathJson = reader.GetString(reader.GetOrdinal("optimal_path"));

        var nodes = PostgresJson.Deserialize<List<NodeRow>>(nodesJson)
            .Select(n => new PuzzleNode(n.Id, n.Title, n.X, n.Y, PostgresJson.ParseKind(n.Kind)))
            .ToList();
        var edges = PostgresJson.Deserialize<List<EdgeRow>>(edgesJson)
            .Select(e => new PuzzleEdge(e.From, e.To))
            .ToList();
        var optimal = PostgresJson.Deserialize<List<string>>(pathJson);

        return new DailyPuzzle
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            PuzzleDate = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("puzzle_date")),
            GraphVersion = reader.GetString(reader.GetOrdinal("graph_version")),
            StartArticleId = reader.GetString(reader.GetOrdinal("start_article_id")),
            TargetArticleId = reader.GetString(reader.GetOrdinal("target_article_id")),
            Nodes = nodes,
            Edges = edges,
            OptimalPath = optimal,
            OptimalLength = reader.GetInt32(reader.GetOrdinal("optimal_length")),
            GeneratorSeed = reader.GetString(reader.GetOrdinal("generator_seed")),
            CorpusVersion = reader.GetString(reader.GetOrdinal("corpus_version")),
            PublishedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("published_at")),
            CreatedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("created_at"))
        };
    }
}
