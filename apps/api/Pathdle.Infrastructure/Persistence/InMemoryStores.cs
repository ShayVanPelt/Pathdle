using System.Collections.Concurrent;
using Pathdle.Application.Abstractions;
using Pathdle.Application.Models;
using Pathdle.Infrastructure.Seed;

namespace Pathdle.Infrastructure.Persistence;

public sealed class SystemClock : IClock
{
    public DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow);
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// In-memory store for local MVP. Replace with Postgres when Supabase is connected.
/// Today's puzzle is always the frozen seed (any UTC date).
/// </summary>
public sealed class InMemoryPuzzleRepository(IClock clock) : IPuzzleRepository
{
    private readonly ConcurrentDictionary<Guid, DailyPuzzle> _byId = new();
    private readonly object _gate = new();

    public Task<DailyPuzzle?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var puzzle = EnsurePuzzleForDate(date);
        return Task.FromResult<DailyPuzzle?>(puzzle);
    }

    public Task<DailyPuzzle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (_byId.TryGetValue(id, out var puzzle))
        {
            return Task.FromResult<DailyPuzzle?>(puzzle);
        }

        // Seed id is stable; materialize for today if requested directly.
        if (id == SeedPuzzleFactory.PuzzleId)
        {
            var today = EnsurePuzzleForDate(clock.UtcToday);
            return Task.FromResult<DailyPuzzle?>(today);
        }

        return Task.FromResult<DailyPuzzle?>(null);
    }

    private DailyPuzzle EnsurePuzzleForDate(DateOnly date)
    {
        lock (_gate)
        {
            var existing = _byId.Values.FirstOrDefault(p => p.PuzzleDate == date);
            if (existing is not null)
            {
                return existing;
            }

            var puzzle = SeedPuzzleFactory.CreateForDate(date);
            // Keep a stable id for the seed so clients can cache; one logical seed puzzle.
            // For in-memory, each date gets its own row clone with the same content/id.
            // Using same Id across dates would collide in _byId — use date-specific id derived from seed.
            if (date == clock.UtcToday)
            {
                _byId[puzzle.Id] = puzzle;
                return puzzle;
            }

            var dated = new DailyPuzzle
            {
                Id = Guid.CreateVersion7(),
                PuzzleDate = date,
                GraphVersion = puzzle.GraphVersion,
                StartArticleId = puzzle.StartArticleId,
                TargetArticleId = puzzle.TargetArticleId,
                Nodes = puzzle.Nodes,
                Edges = puzzle.Edges,
                OptimalPath = puzzle.OptimalPath,
                OptimalLength = puzzle.OptimalLength,
                GeneratorSeed = puzzle.GeneratorSeed,
                CorpusVersion = puzzle.CorpusVersion,
                PublishedAt = puzzle.PublishedAt,
                CreatedAt = puzzle.CreatedAt
            };
            _byId[dated.Id] = dated;
            return dated;
        }
    }
}

public sealed class InMemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<Guid, PlayerGame> _byId = new();

    public Task<PlayerGame?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _byId.TryGetValue(id, out var game);
        return Task.FromResult(CloneOrNull(game));
    }

    public Task<PlayerGame?> GetByPuzzleAndPlayerAsync(
        Guid puzzleId,
        string playerKey,
        CancellationToken cancellationToken = default)
    {
        var game = _byId.Values.FirstOrDefault(g =>
            g.DailyPuzzleId == puzzleId
            && string.Equals(g.PlayerKey, playerKey, StringComparison.Ordinal));
        return Task.FromResult(CloneOrNull(game));
    }

    public Task AddAsync(PlayerGame game, CancellationToken cancellationToken = default)
    {
        if (!_byId.TryAdd(game.Id, Clone(game)))
        {
            throw new InvalidOperationException($"Game {game.Id} already exists.");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(PlayerGame game, CancellationToken cancellationToken = default)
    {
        _byId[game.Id] = Clone(game);
        return Task.CompletedTask;
    }

    private static PlayerGame? CloneOrNull(PlayerGame? game) => game is null ? null : Clone(game);

    private static PlayerGame Clone(PlayerGame game) =>
        new()
        {
            Id = game.Id,
            DailyPuzzleId = game.DailyPuzzleId,
            PlayerKey = game.PlayerKey,
            Status = game.Status,
            DiscoveredEdges = game.DiscoveredEdges.Select(e => new PuzzleEdge(e.From, e.To)).ToList(),
            HintEdges = game.HintEdges.Select(e => new PuzzleEdge(e.From, e.To)).ToList(),
            AttemptedEdges = game.AttemptedEdges
                .Select(a => new AttemptedEdge(a.From, a.To, a.Success, a.AttemptedAt))
                .ToList(),
            PlayerPath = [.. game.PlayerPath],
            RevealedArticleIds = [.. game.RevealedArticleIds],
            Score = game.Score,
            ConnectionCount = game.ConnectionCount,
            StartedAt = game.StartedAt,
            CompletedAt = game.CompletedAt
        };
}
