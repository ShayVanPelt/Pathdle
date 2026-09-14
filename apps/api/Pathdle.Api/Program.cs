using Pathdle.Application;
using Pathdle.Application.Dtos;
using Pathdle.Application.Services;
using Pathdle.Infrastructure;

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? ["http://localhost:3000"];
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();
var storageMode = Pathdle.Infrastructure.DependencyInjection.GetStorageMode(builder.Configuration);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapGet("/", () => Results.Ok(new
{
    service = "Pathdle.Api",
    storage = storageMode,
    docs = new
    {
        health = "/api/health",
        today = "/api/puzzles/today",
        startGame = "POST /api/games (header X-Player-Key required)"
    }
}));

app.MapGet("/api/health", async (HttpContext ctx, CancellationToken ct) =>
{
    object? db = null;
    if (string.Equals(storageMode, Pathdle.Infrastructure.DependencyInjection.StoragePostgres, StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var dataSource = ctx.RequestServices.GetRequiredService<Npgsql.NpgsqlDataSource>();
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "select 1";
            await cmd.ExecuteScalarAsync(ct);
            db = "up";
        }
        catch (Exception ex)
        {
            db = $"down: {ex.Message}";
        }
    }

    return Results.Ok(new
    {
        status = "ok",
        service = "Pathdle.Api",
        storage = storageMode,
        database = db
    });
});

app.MapGet("/api/puzzles/today", async (GameService games, CancellationToken ct) =>
{
    var puzzle = await games.GetTodayPuzzleAsync(ct);
    return puzzle is null ? Results.NotFound(new { error = "No puzzle for today." }) : Results.Ok(puzzle);
});

app.MapGet("/api/puzzles/{date}", async (DateOnly date, GameService games, CancellationToken ct) =>
{
    var puzzle = await games.GetPuzzleByDateAsync(date, ct);
    return puzzle is null ? Results.NotFound(new { error = "Puzzle not found." }) : Results.Ok(puzzle);
});

app.MapPost("/api/games", async (
    StartGameRequest? body,
    HttpRequest request,
    GameService games,
    CancellationToken ct) =>
{
    try
    {
        var playerKey = RequirePlayerKey(request);
        var state = await games.StartGameAsync(playerKey, body?.PuzzleId, ct);
        return Results.Ok(state);
    }
    catch (AppException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: ex.StatusCode);
    }
});

app.MapGet("/api/games/{gameId:guid}", async (
    Guid gameId,
    HttpRequest request,
    GameService games,
    CancellationToken ct) =>
{
    try
    {
        var playerKey = RequirePlayerKey(request);
        var state = await games.GetGameAsync(gameId, playerKey, ct);
        return Results.Ok(state);
    }
    catch (AppException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: ex.StatusCode);
    }
});

app.MapPost("/api/games/{gameId:guid}/attempts", async (
    Guid gameId,
    AttemptRequest body,
    HttpRequest request,
    GameService games,
    CancellationToken ct) =>
{
    try
    {
        var playerKey = RequirePlayerKey(request);
        var result = await games.AttemptConnectionAsync(gameId, playerKey, body, ct);
        return Results.Ok(result);
    }
    catch (AppException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: ex.StatusCode);
    }
});

app.MapPost("/api/games/{gameId:guid}/reveals", async (
    Guid gameId,
    RevealRequest body,
    HttpRequest request,
    GameService games,
    CancellationToken ct) =>
{
    try
    {
        var playerKey = RequirePlayerKey(request);
        var result = await games.RevealOutboundAsync(gameId, playerKey, body, ct);
        return Results.Ok(result);
    }
    catch (AppException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: ex.StatusCode);
    }
});

app.MapPost("/api/games/{gameId:guid}/complete", async (
    Guid gameId,
    HttpRequest request,
    GameService games,
    CancellationToken ct) =>
{
    try
    {
        var playerKey = RequirePlayerKey(request);
        var result = await games.CompleteGameAsync(gameId, playerKey, ct);
        return Results.Ok(result);
    }
    catch (AppException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: ex.StatusCode);
    }
});

app.Run();

static void LoadDotEnv()
{
    // Single monorepo .env at repo root (folder that contains Pathdle.sln).
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

    // Fallback: .env next to cwd (e.g. if opened only on the Api project).
    var local = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (File.Exists(local))
    {
        DotNetEnv.Env.Load(local);
    }
}

static string RequirePlayerKey(HttpRequest request)
{
    if (!request.Headers.TryGetValue("X-Player-Key", out var values)
        || string.IsNullOrWhiteSpace(values.FirstOrDefault()))
    {
        throw new ValidationException("X-Player-Key header is required.");
    }

    return values.ToString();
}
