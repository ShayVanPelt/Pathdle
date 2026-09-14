using Pathdle.Application.Models;

namespace Pathdle.Application.Abstractions;

public interface IPuzzleRepository
{
    Task<DailyPuzzle?> GetByDateAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<DailyPuzzle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IGameRepository
{
    Task<PlayerGame?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlayerGame?> GetByPuzzleAndPlayerAsync(Guid puzzleId, string playerKey, CancellationToken cancellationToken = default);
    Task AddAsync(PlayerGame game, CancellationToken cancellationToken = default);
    Task UpdateAsync(PlayerGame game, CancellationToken cancellationToken = default);
}

public interface IClock
{
    DateOnly UtcToday { get; }
    DateTimeOffset UtcNow { get; }
}
