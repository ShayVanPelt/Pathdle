using Microsoft.Extensions.Configuration;

namespace Pathdle.Generator;

internal static class EnvBootstrap
{
    public static IConfiguration Load()
    {
        LoadRootEnv();
        return new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
    }

    private static void LoadRootEnv()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            var sln = Path.Combine(dir.FullName, "Pathdle.sln");
            var envPath = Path.Combine(dir.FullName, ".env");
            if (File.Exists(sln) && File.Exists(envPath))
            {
                DotNetEnv.Env.Load(envPath);
                return;
            }

            dir = dir.Parent;
        }
    }

    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Pathdle.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
