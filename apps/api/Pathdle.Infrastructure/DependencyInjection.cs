using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pathdle.Application.Abstractions;
using Pathdle.Infrastructure.Persistence;
using Pathdle.Infrastructure.Persistence.Postgres;

namespace Pathdle.Infrastructure;

public static class DependencyInjection
{
    public const string StorageInMemory = "InMemory";
    public const string StoragePostgres = "Postgres";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();

        var storage = configuration["Pathdle:Storage"] ?? StorageInMemory;
        if (string.Equals(storage, StoragePostgres, StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = configuration.GetConnectionString("Postgres");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Pathdle:Storage=Postgres requires ConnectionStrings:Postgres.");
            }

            services.AddSingleton(NpgsqlDataSource.Create(connectionString));
            services.AddScoped<PostgresPuzzleRepository>();
            services.AddScoped<IPuzzleRepository>(sp => sp.GetRequiredService<PostgresPuzzleRepository>());
            services.AddScoped<IGameRepository, PostgresGameRepository>();
            services.AddHostedService<EnsureSeedPuzzleHostedService>();
        }
        else
        {
            services.AddSingleton<IPuzzleRepository, InMemoryPuzzleRepository>();
            services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        }

        return services;
    }

    public static string GetStorageMode(IConfiguration configuration) =>
        configuration["Pathdle:Storage"] ?? StorageInMemory;
}
