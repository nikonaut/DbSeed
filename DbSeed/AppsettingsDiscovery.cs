using System.Text.Json;

namespace DbSeed;

internal static class AppsettingsDiscovery
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public static string ResolveAppsettings(string projectDirectory, string? appsettingsFile)
    {
        if (!string.IsNullOrWhiteSpace(appsettingsFile))
        {
            var file = Path.IsPathRooted(appsettingsFile)
                ? Path.GetFullPath(appsettingsFile)
                : Path.GetFullPath(appsettingsFile, projectDirectory);

            if (!File.Exists(file))
            {
                throw new DbSeedException($"Appsettings file '{file}' does not exist.");
            }

            return file;
        }

        var candidates = Directory
            .EnumerateFiles(projectDirectory, "appsettings*.json", SearchOption.TopDirectoryOnly)
            .Where(file => ReadConnectionStrings(file).Count > 0)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
        {
            throw new DbSeedException(
                $"No appsettings file with connection string data was found in '{projectDirectory}', so the command could not execute.");
        }

        if (candidates.Length > 1)
        {
            var names = string.Join(", ", candidates.Select(Path.GetFileName));
            throw new DbSeedException(
                $"Multiple appsettings files with connection string data are available ({names}). Specify one with -a {{appsetting-file}} or --appsettings {{appsetting-file}}.");
        }

        return candidates[0];
    }

    public static Dictionary<string, string> ReadConnectionStrings(string appsettingsFile)
    {
        using var stream = File.OpenRead(appsettingsFile);
        using var json = JsonDocument.Parse(stream, JsonOptions);

        if (!TryGetPropertyIgnoreCase(json.RootElement, "ConnectionStrings", out var section) ||
            section.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in section.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var value = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values[property.Name] = value;
                }
            }
        }

        return values;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
