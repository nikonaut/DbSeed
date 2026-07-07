using Microsoft.Data.SqlClient;

namespace DbSeed;

internal static class SqlServerSchema
{
    public static async Task<IReadOnlyList<SqlTableName>> ListTablesAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.name AS [schema_name], t.name AS [table_name]
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name;
            """;

        var tables = new List<SqlTableName>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(new SqlTableName(reader.GetString(0), reader.GetString(1)));
        }

        return tables;
    }

    public static async Task<List<ExportColumn>> ListColumnsForExportAsync(SqlConnection connection, SqlTableName table)
    {
        var columns = await ReadColumnsAsync(connection, table, transaction: null);
        return columns.Values
            .OrderBy(column => column.Ordinal)
            .Select(column => new ExportColumn(column.Name, column.DataType, column.IsIdentity, column.IsComputed))
            .ToList();
    }

    public static Task<IReadOnlyDictionary<string, SqlColumnInfo>> ListColumnsAsync(
        SqlConnection connection,
        SqlTableName table,
        SqlTransaction transaction) =>
        ReadColumnsAsync(connection, table, transaction);

    private static async Task<IReadOnlyDictionary<string, SqlColumnInfo>> ReadColumnsAsync(
        SqlConnection connection,
        SqlTableName table,
        SqlTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT c.name, ty.name, c.is_identity, c.is_computed, c.column_id
            FROM sys.columns c
            INNER JOIN sys.types ty ON ty.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(@tableName)
            ORDER BY c.column_id;
            """;
        command.Parameters.AddWithValue("@tableName", table.FullName);

        var columns = new Dictionary<string, SqlColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var column = new SqlColumnInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetBoolean(2),
                reader.GetBoolean(3),
                reader.GetInt32(4));
            columns[column.Name] = column;
        }

        if (columns.Count == 0)
        {
            throw new DbSeedException($"Table '{table.FullName}' was not found in the database.");
        }

        return columns;
    }

    public static string QuoteIdentifier(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
