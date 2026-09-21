using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using Pathdle.Application.Models;

namespace Pathdle.Generator.Persistence;

internal static class PuzzlePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<bool> TryPublishAsync(
        string connectionString,
        DailyPuzzle puzzle,
        CancellationToken ct)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        await using (var exists = new NpgsqlCommand(
                         "select 1 from daily_puzzles where puzzle_date = @date",
                         conn))
        {
            exists.Parameters.AddWithValue("date", puzzle.PuzzleDate);
            var found = await exists.ExecuteScalarAsync(ct);
            if (found is not null)
            {
                Console.WriteLine($"Skip: puzzle already published for {puzzle.PuzzleDate:yyyy-MM-dd}.");
                return false;
            }
        }

        var nodeRows = puzzle.Nodes.Select(n => new
        {
            id = n.Id,
            title = n.Title,
            x = n.X,
            y = n.Y,
            kind = n.Kind switch
            {
                NodeKind.Start => "start",
                NodeKind.Target => "target",
                _ => "normal"
            },
            description = n.Description
        });
        var edgeRows = puzzle.Edges.Select(e => new
        {
            from = e.From,
            to = e.To,
            groupId = e.GroupId,
            groupLabel = e.GroupLabel
        });

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

        cmd.Parameters.AddWithValue("id", puzzle.Id);
        cmd.Parameters.AddWithValue("puzzle_date", puzzle.PuzzleDate);
        cmd.Parameters.AddWithValue("graph_version", puzzle.GraphVersion);
        cmd.Parameters.AddWithValue("start_article_id", puzzle.StartArticleId);
        cmd.Parameters.AddWithValue("target_article_id", puzzle.TargetArticleId);
        cmd.Parameters.AddWithValue("nodes", JsonSerializer.Serialize(nodeRows, JsonOptions));
        cmd.Parameters.AddWithValue("edges", JsonSerializer.Serialize(edgeRows, JsonOptions));
        cmd.Parameters.AddWithValue("optimal_path", JsonSerializer.Serialize(puzzle.OptimalPath, JsonOptions));
        cmd.Parameters.AddWithValue("optimal_length", puzzle.OptimalLength);
        cmd.Parameters.AddWithValue(
            "difficulty",
            JsonSerializer.Serialize(puzzle.Difficulty ?? new { note = "generated" }, JsonOptions));
        cmd.Parameters.AddWithValue("generator_seed", puzzle.GeneratorSeed);
        cmd.Parameters.AddWithValue("corpus_version", puzzle.CorpusVersion);
        cmd.Parameters.AddWithValue("published_at", puzzle.PublishedAt);
        cmd.Parameters.AddWithValue("created_at", puzzle.CreatedAt);

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected > 0)
        {
            Console.WriteLine($"Published puzzle {puzzle.Id} for {puzzle.PuzzleDate:yyyy-MM-dd}.");
            return true;
        }

        Console.WriteLine($"Skip: conflict on {puzzle.PuzzleDate:yyyy-MM-dd}.");
        return false;
    }

    public static async Task<DateOnly> FindNextFreeDateAsync(
        string connectionString,
        DateOnly floor,
        CancellationToken ct)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var occupied = new HashSet<DateOnly>();
        await using (var cmd = new NpgsqlCommand(
                         """
                         select puzzle_date
                         from daily_puzzles
                         where puzzle_date >= @floor
                         order by puzzle_date
                         """,
                         conn))
        {
            cmd.Parameters.AddWithValue("floor", floor);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                occupied.Add(DateOnly.FromDateTime(reader.GetDateTime(0)));
            }
        }

        var candidate = floor;
        while (occupied.Contains(candidate))
        {
            candidate = candidate.AddDays(1);
        }

        return candidate;
    }
}
