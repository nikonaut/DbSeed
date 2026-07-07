using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace DbSeed;

internal static class ExportCommand
{
    public static async Task<int> ExecuteAsync(string connectionString, CliOptions options)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var tables = await SqlServerSchema.ListTablesAsync(connection);
        var selectedTables = FilterTables(tables, options.IncludeTables, options.ExcludeTables).ToArray();

        var document = new ExportDocument();
        foreach (var table in selectedTables)
        {
            var columns = await SqlServerSchema.ListColumnsForExportAsync(connection, table);
            var rows = await ReadRowsAsync(connection, table);
            document.Tables.Add(new ExportTable(table.Schema, table.Name, columns, rows));
        }

        var json = JsonSerializer.Serialize(document, JsonOptions.CreateIndented());
        if (string.IsNullOrWhiteSpace(options.OutputFile))
        {
            Console.WriteLine(json);
        }
        else
        {
            var output = Path.GetFullPath(options.OutputFile, Directory.GetCurrentDirectory());
            var parent = Path.GetDirectoryName(output);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            await File.WriteAllTextAsync(output, json + Environment.NewLine);
            Console.WriteLine($"Exported {selectedTables.Length} table(s) to {output}.");
        }

        return 0;
    }

    internal static IEnumerable<SqlTableName> FilterTables(
        IReadOnlyList<SqlTableName> tables,
        IReadOnlySet<string> include,
        IReadOnlySet<string> exclude)
    {
        foreach (var table in tables)
        {
            if (include.Count > 0 && !include.Any(name => TableNameMatcher.Matches(name, table)))
            {
                continue;
            }

            if (exclude.Any(name => TableNameMatcher.Matches(name, table)))
            {
                continue;
            }

            yield return table;
        }
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(SqlConnection connection, SqlTableName table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {table.QuotedName};";

        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
            {
                var value = await reader.IsDBNullAsync(ordinal) ? null : reader.GetValue(ordinal);
                row[reader.GetName(ordinal)] = NormalizeValue(value);
            }

            rows.Add(row);
        }

        return rows;
    }

    internal static object? NormalizeValue(object? value) => value switch
    {
        null => null,
        byte[] bytes => Convert.ToBase64String(bytes),
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
        TimeSpan timeSpan => timeSpan.ToString("c", CultureInfo.InvariantCulture),
        Guid guid => guid.ToString("D"),
        _ => value
    };
}
