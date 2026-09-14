using Npgsql;
using Pathdle.Application.Abstractions;
using Pathdle.Application.Models;

namespace Pathdle.Infrastructure.Persistence.Postgres;

public sealed class PostgresGameRepository(NpgsqlDataSource dataSource) : IGameRepository
{
    public async Task<PlayerGame?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            select id, daily_puzzle_id, player_key, status, discovered_edges, hint_edges,
                   attempted_edges, player_path, score, connection_count, reveals,
                   started_at, completed_at
            from player_games
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

    public async Task<PlayerGame?> GetByPuzzleAndPlayerAsync(
        Guid puzzleId,
        string playerKey,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            select id, daily_puzzle_id, player_key, status, discovered_edges, hint_edges,
                   attempted_edges, player_path, score, connection_count, reveals,
                   started_at, completed_at
            from player_games
            where daily_puzzle_id = @puzzle_id and player_key = @player_key
            """,
            conn);
        cmd.Parameters.AddWithValue("puzzle_id", puzzleId);
        cmd.Parameters.AddWithValue("player_key", playerKey);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task AddAsync(PlayerGame game, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            insert into player_games (
              id, daily_puzzle_id, player_key, status, discovered_edges, hint_edges,
              attempted_edges, player_path, score, connection_count, reveals,
              started_at, completed_at
            ) values (
              @id, @daily_puzzle_id, @player_key, @status,
              @discovered_edges::jsonb, @hint_edges::jsonb, @attempted_edges::jsonb,
              @player_path::jsonb, @score, @connection_count, @reveals::jsonb,
              @started_at, @completed_at
            )
            """,
            conn);
        Bind(cmd, game);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(PlayerGame game, CancellationToken cancellationToken = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            """
            update player_games set
              status = @status,
              discovered_edges = @discovered_edges::jsonb,
              hint_edges = @hint_edges::jsonb,
              attempted_edges = @attempted_edges::jsonb,
              player_path = @player_path::jsonb,
              score = @score,
              connection_count = @connection_count,
              reveals = @reveals::jsonb,
              completed_at = @completed_at
            where id = @id
            """,
            conn);
        Bind(cmd, game);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (affected == 0)
        {
            throw new InvalidOperationException($"Game {game.Id} was not found for update.");
        }
    }

    private static void Bind(NpgsqlCommand cmd, PlayerGame game)
    {
        cmd.Parameters.AddWithValue("id", game.Id);
        cmd.Parameters.AddWithValue("daily_puzzle_id", game.DailyPuzzleId);
        cmd.Parameters.AddWithValue("player_key", game.PlayerKey);
        cmd.Parameters.AddWithValue("status", PostgresJson.FormatStatus(game.Status));
        cmd.Parameters.AddWithValue(
            "discovered_edges",
            PostgresJson.Serialize(game.DiscoveredEdges.Select(e => new EdgeRow(e.From, e.To)).ToList()));
        cmd.Parameters.AddWithValue(
            "hint_edges",
            PostgresJson.Serialize(game.HintEdges.Select(e => new EdgeRow(e.From, e.To)).ToList()));
        cmd.Parameters.AddWithValue(
            "attempted_edges",
            PostgresJson.Serialize(
                game.AttemptedEdges
                    .Select(a => new AttemptRow(a.From, a.To, a.Success, a.AttemptedAt))
                    .ToList()));
        cmd.Parameters.AddWithValue("player_path", PostgresJson.Serialize(game.PlayerPath));
        cmd.Parameters.AddWithValue("score", game.Score);
        cmd.Parameters.AddWithValue("connection_count", game.ConnectionCount);
        cmd.Parameters.AddWithValue("reveals", PostgresJson.Serialize(game.RevealedArticleIds));
        cmd.Parameters.AddWithValue("started_at", game.StartedAt);
        cmd.Parameters.AddWithValue("completed_at", (object?)game.CompletedAt ?? DBNull.Value);
    }

    private static PlayerGame Map(NpgsqlDataReader reader)
    {
        var discovered = PostgresJson
            .Deserialize<List<EdgeRow>>(reader.GetString(reader.GetOrdinal("discovered_edges")), [])
            .Select(e => new PuzzleEdge(e.From, e.To))
            .ToList();
        var hints = PostgresJson
            .Deserialize<List<EdgeRow>>(reader.GetString(reader.GetOrdinal("hint_edges")), [])
            .Select(e => new PuzzleEdge(e.From, e.To))
            .ToList();
        var attempted = PostgresJson
            .Deserialize<List<AttemptRow>>(reader.GetString(reader.GetOrdinal("attempted_edges")), [])
            .Select(a => new AttemptedEdge(a.From, a.To, a.Success, a.AttemptedAt))
            .ToList();
        var path = PostgresJson.Deserialize<List<string>>(
            reader.GetString(reader.GetOrdinal("player_path")),
            []);
        var reveals = PostgresJson.Deserialize<List<string>>(
            reader.GetString(reader.GetOrdinal("reveals")),
            []);

        var completedOrdinal = reader.GetOrdinal("completed_at");
        return new PlayerGame
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            DailyPuzzleId = reader.GetGuid(reader.GetOrdinal("daily_puzzle_id")),
            PlayerKey = reader.GetString(reader.GetOrdinal("player_key")),
            Status = PostgresJson.ParseStatus(reader.GetString(reader.GetOrdinal("status"))),
            DiscoveredEdges = discovered,
            HintEdges = hints,
            AttemptedEdges = attempted,
            PlayerPath = path,
            RevealedArticleIds = reveals,
            Score = reader.GetInt32(reader.GetOrdinal("score")),
            ConnectionCount = reader.GetInt32(reader.GetOrdinal("connection_count")),
            StartedAt = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("started_at")),
            CompletedAt = reader.IsDBNull(completedOrdinal)
                ? null
                : reader.GetFieldValue<DateTimeOffset>(completedOrdinal)
        };
    }
}
