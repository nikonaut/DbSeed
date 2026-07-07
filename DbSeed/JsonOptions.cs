using System.Text.Json;

namespace DbSeed;

internal static class JsonOptions
{
    public static JsonSerializerOptions CreateIndented() => new()
    {
        WriteIndented = true
    };
}
