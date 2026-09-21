using System.Text.Json;
using System.Text.Json.Serialization;
using Pathdle.Application.Models;

namespace Pathdle.Infrastructure.Persistence.Postgres;

internal static class PostgresJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}.");

    public static T Deserialize<T>(string? json, T fallback) =>
        string.IsNullOrWhiteSpace(json)
            ? fallback
            : JsonSerializer.Deserialize<T>(json, Options) ?? fallback;

    public static NodeKind ParseKind(string? kind) =>
        kind?.ToLowerInvariant() switch
        {
            "start" => NodeKind.Start,
            "target" => NodeKind.Target,
            _ => NodeKind.Normal
        };

    public static string FormatKind(NodeKind kind) =>
        kind switch
        {
            NodeKind.Start => "start",
            NodeKind.Target => "target",
            _ => "normal"
        };

    public static GameStatus ParseStatus(string? status) =>
        status?.ToLowerInvariant() switch
        {
            "completed" => GameStatus.Completed,
            "abandoned" => GameStatus.Abandoned,
            _ => GameStatus.Active
        };

    public static string FormatStatus(GameStatus status) =>
        status switch
        {
            GameStatus.Completed => "completed",
            GameStatus.Abandoned => "abandoned",
            _ => "active"
        };
}

internal sealed record NodeRow(
    string Id,
    string Title,
    double X,
    double Y,
    string Kind,
    string? Description = null);
internal sealed record EdgeRow(string From, string To, string? GroupId = null, string? GroupLabel = null);
internal sealed record AttemptRow(string From, string To, bool Success, DateTimeOffset AttemptedAt);
