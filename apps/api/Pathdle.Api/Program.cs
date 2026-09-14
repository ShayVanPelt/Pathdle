using Pathdle.Application;
using Pathdle.Application.Dtos;
using Pathdle.Application.Services;
using Pathdle.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

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
    docs = new
    {
        health = "/api/health",
        today = "/api/puzzles/today",
        startGame = "POST /api/games (header X-Player-Key required)"
    }
}));

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "Pathdle.Api",
    storage = "in-memory-seed"
}));

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

static string RequirePlayerKey(HttpRequest request)
{
    if (!request.Headers.TryGetValue("X-Player-Key", out var values)
        || string.IsNullOrWhiteSpace(values.FirstOrDefault()))
    {
        throw new ValidationException("X-Player-Key header is required.");
    }

    return values.ToString();
}
