using Microsoft.Extensions.DependencyInjection;
using Pathdle.Application.Services;

namespace Pathdle.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GameService>();
        return services;
    }
}
