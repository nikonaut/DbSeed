using System.Globalization;
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace DbSeed;

internal static class ImportCommand
{
    public static async Task<int> ExecuteAsync(string connectionString, CliOptions options)
    {
        var input = Path.GetFullPath(options.InputFile!, Directory.GetCurrentDirectory());
        if (!File.Exists(input))
        {
            throw new DbSeedException($"Input file '{input}' does not exist.");
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        var inserted = 0;
        try
        {
            using var stream = File.OpenRead(input);
            using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            var tablesElement = ReadTables(document.RootElement);

            foreach (var tableElement in tablesElement.EnumerateArray())
            {
                var table = ReadTableName(tableElement);
                if (options.ExcludeTables.Any(name => TableNameMatcher.Matches(name, table)))
                {
                    continue;
                }

                var columns = await SqlServerSchema.ListColumnsAsync(connection, table, transaction);
                if (options.CleanBeforeImport)
                {
                    await CleanTableAsync(connection, transaction, table);
                }

                inserted += await InsertRowsAsync(connection, transaction, table, columns, tableElement);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        Console.WriteLine($"Imported {inserted} row(s) from {input}.");
        return 0;
    }

    internal static JsonElement ReadTables(JsonElement root)
    {
        if (!TryGetProperty(root, "tables", out var tablesElement) ||
            tablesElement.ValueKind != JsonValueKind.Array)
        {
            throw new DbSeedException("The input file is not a DbSeed export: root property 'tables' is missing.");
        }

        return tablesElement;
    }

    private static async Task CleanTableAsync(SqlConnection connection, SqlTransaction transaction, SqlTableName table) =>
        await ExecuteNonQueryAsync(connection, transaction, BuildCleanTableCommandText(table));

    internal static string BuildCleanTableCommandText(SqlTableName table) =>
        $"DELETE FROM {table.QuotedName};";

    internal static SqlTableName ReadTableName(JsonElement tableElement)
    {
        var schema = TryGetProperty(tableElement, "schema", out var schemaElement) && schemaElement.ValueKind == JsonValueKind.String
            ? schemaElement.GetString()
            : "dbo";

        if (!TryGetProperty(tableElement, "name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(nameElement.GetString()))
        {
            throw new DbSeedException("A table entry in the export is missing a table name.");
        }

        return new SqlTableName(schema ?? "dbo", nameElement.GetString()!);
    }

    private static async Task<int> InsertRowsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SqlTableName table,
        IReadOnlyDictionary<string, SqlColumnInfo> columns,
        JsonElement tableElement)
    {
        if (!TryGetProperty(tableElement, "rows", out var rowsElement) || rowsElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var inserted = 0;
        var identityColumn = columns.Values.FirstOrDefault(column => column.IsIdentity);
        var useIdentityInsert = identityColumn is not null && rowsElement.EnumerateArray().Any(row =>
            row.ValueKind == JsonValueKind.Object &&
            row.EnumerateObject().Any(property => string.Equals(property.Name, identityColumn.Name, StringComparison.OrdinalIgnoreCase)));

        if (useIdentityInsert)
        {
            await ExecuteNonQueryAsync(connection, transaction, $"SET IDENTITY_INSERT {table.QuotedName} ON;");
        }

        try
        {
            foreach (var row in rowsElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                {
                    throw new DbSeedException($"A row in table '{table.FullName}' is not a JSON object.");
                }

                var insertable = new List<(SqlColumnInfo Column, JsonElement Value)>();
                foreach (var property in row.EnumerateObject())
                {
                    if (columns.TryGetValue(property.Name, out var column) && IsInsertable(column))
                    {
                        insertable.Add((column, property.Value));
                    }
                }

                if (insertable.Count == 0)
                {
                    continue;
                }

                insertable.Sort((left, right) => left.Column.Ordinal.CompareTo(right.Column.Ordinal));
                await InsertRowAsync(connection, transaction, table, insertable);
                inserted++;
            }
        }
        finally
        {
            if (useIdentityInsert)
            {
                await ExecuteNonQueryAsync(connection, transaction, $"SET IDENTITY_INSERT {table.QuotedName} OFF;");
            }
        }

        return inserted;
    }

    private static async Task InsertRowAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SqlTableName table,
        IReadOnlyList<(SqlColumnInfo Column, JsonElement Value)> values)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO {table.QuotedName} ({string.Join(", ", values.Select(value => SqlServerSchema.QuoteIdentifier(value.Column.Name)))})
            VALUES ({string.Join(", ", values.Select((_, index) => $"@p{index}"))});
            """;

        for (var index = 0; index < values.Count; index++)
        {
            var (column, value) = values[index];
            command.Parameters.Add(CreateParameter($"@p{index}", ConvertJsonValue(value, column), column));
        }

        await command.ExecuteNonQueryAsync();
    }

    internal static SqlParameter CreateParameter(string name, object value, SqlColumnInfo column)
    {
        var sqlDbType = column.DataType.ToLowerInvariant() switch
        {
            "date" => SqlDbType.Date,
            "datetime" => SqlDbType.DateTime,
            "datetime2" => SqlDbType.DateTime2,
            "datetimeoffset" => SqlDbType.DateTimeOffset,
            "smalldatetime" => SqlDbType.SmallDateTime,
            "time" => SqlDbType.Time,
            _ => (SqlDbType?)null
        };

        return sqlDbType is { } temporalType
            ? new SqlParameter(name, temporalType) { Value = value }
            : new SqlParameter(name, value);
    }

    internal static object ConvertJsonValue(JsonElement value, SqlColumnInfo column)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return DBNull.Value;
        }

        var dataType = column.DataType.ToLowerInvariant();
        if (IsBinary(dataType))
        {
            return Convert.FromBase64String(value.GetString() ?? string.Empty);
        }

        return dataType switch
        {
            "bigint" => value.GetInt64(),
            "int" => value.GetInt32(),
            "smallint" => value.GetInt16(),
            "tinyint" => value.GetByte(),
            "bit" => value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && bool.Parse(value.GetString()!)),
            "decimal" or "numeric" or "money" or "smallmoney" => value.GetDecimal(),
            "float" => value.GetDouble(),
            "real" => value.GetSingle(),
            "uniqueidentifier" => Guid.Parse(value.GetString()!),
            "datetimeoffset" => DateTimeOffset.Parse(value.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "datetime" or "datetime2" or "smalldatetime" or "date" => DateTime.Parse(value.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "time" => TimeSpan.Parse(value.GetString()!, CultureInfo.InvariantCulture),
            _ => JsonValueToClr(value)
        };
    }

    private static object JsonValueToClr(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
        JsonValueKind.Number when value.TryGetDecimal(out var decimalValue) => decimalValue,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => value.GetRawText()
    };

    internal static bool IsInsertable(SqlColumnInfo column) =>
        !column.IsComputed && !IsRowVersion(column.DataType);

    internal static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsRowVersion(string dataType) =>
        dataType.Equals("timestamp", StringComparison.OrdinalIgnoreCase) ||
        dataType.Equals("rowversion", StringComparison.OrdinalIgnoreCase);

    private static bool IsBinary(string dataType) =>
        dataType is "binary" or "varbinary" or "image" or "timestamp" or "rowversion";

    private static async Task ExecuteNonQueryAsync(SqlConnection connection, SqlTransaction transaction, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
