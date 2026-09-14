using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pathdle.Application.Abstractions;
using Pathdle.Infrastructure.Seed;

namespace Pathdle.Infrastructure.Persistence.Postgres;

/// <summary>
/// Ensures today's seed puzzle exists in Postgres for local MVP (never overwrites).
/// </summary>
public sealed class EnsureSeedPuzzleHostedService(
    IServiceProvider services,
    IClock clock,
    ILogger<EnsureSeedPuzzleHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var puzzles = scope.ServiceProvider.GetRequiredService<PostgresPuzzleRepository>();
        var today = clock.UtcToday;
        var existing = await puzzles.GetByDateAsync(today, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation("Postgres puzzle for {Date} already present ({Id}).", today, existing.Id);
            return;
        }

        var seed = SeedPuzzleFactory.CreateForDate(today);
        var inserted = await puzzles.TryInsertAsync(seed, cancellationToken);
        if (inserted)
        {
            logger.LogInformation("Inserted seed puzzle for {Date} ({Id}).", today, seed.Id);
        }
        else
        {
            logger.LogInformation("Seed insert skipped (race) for {Date}.", today);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
