using Microsoft.Extensions.DependencyInjection;
using Pathdle.Application.Abstractions;
using Pathdle.Infrastructure.Persistence;

namespace Pathdle.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPuzzleRepository, InMemoryPuzzleRepository>();
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        return services;
    }
}
