using System.Data;
using System.Text.Json;

namespace DbSeed.Tests;

public sealed class ImportCommandTests
{
    [Fact]
    public void ReadTables_IgnoresUnrelatedRootMetadata()
    {
        using var document = JsonDocument.Parse("""
            {
              "metadata": {
                "description": "Not interpreted during import",
                "additionalProperty": [1, 2, 3]
              },
              "tables": []
            }
            """);

        var tables = ImportCommand.ReadTables(document.RootElement);

        Assert.Equal(JsonValueKind.Array, tables.ValueKind);
    }

    [Fact]
    public void ReadTableName_WithExplicitSchemaAndName_ReturnsTable()
    {
        using var document = JsonDocument.Parse("""
            { "schema": "identity", "name": "Users" }
            """);

        var table = ImportCommand.ReadTableName(document.RootElement);

        Assert.Equal("identity", table.Schema);
        Assert.Equal("Users", table.Name);
    }

    [Fact]
    public void ReadTableName_WithExportPropertyCasing_ReturnsTable()
    {
        using var document = JsonDocument.Parse("""
            { "Schema": "identity", "Name": "Users" }
            """);

        var table = ImportCommand.ReadTableName(document.RootElement);

        Assert.Equal("identity", table.Schema);
        Assert.Equal("Users", table.Name);
    }

    [Fact]
    public void ReadTableName_WithoutSchema_DefaultsToDbo()
    {
        using var document = JsonDocument.Parse("""
            { "name": "Users" }
            """);

        var table = ImportCommand.ReadTableName(document.RootElement);

        Assert.Equal("dbo", table.Schema);
        Assert.Equal("Users", table.Name);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"name\":\"\"}")]
    [InlineData("{\"name\":42}")]
    public void ReadTableName_WithMissingOrInvalidName_Throws(string json)
    {
        using var document = JsonDocument.Parse(json);

        var exception = Assert.Throws<DbSeedException>(() => ImportCommand.ReadTableName(document.RootElement));

        Assert.Contains("missing a table name", exception.Message);
    }

    [Fact]
    public void BuildCleanTableCommandText_QuotesTableName()
    {
        var table = new SqlTableName("identity", "User]Roles");

        var commandText = ImportCommand.BuildCleanTableCommandText(table);

        Assert.Equal("DELETE FROM [identity].[User]]Roles];", commandText);
    }

    [Theory]
    [InlineData("bigint", "9223372036854775807", 9223372036854775807L)]
    [InlineData("int", "2147483647", 2147483647)]
    [InlineData("smallint", "32767", (short)32767)]
    [InlineData("tinyint", "255", (byte)255)]
    [InlineData("decimal", "123.45", 123.45)]
    [InlineData("numeric", "123.45", 123.45)]
    [InlineData("money", "123.45", 123.45)]
    [InlineData("float", "123.45", 123.45)]
    [InlineData("real", "123.5", 123.5f)]
    public void ConvertJsonValue_ConvertsNumericTypes(string sqlType, string json, object expected)
    {
        using var document = JsonDocument.Parse(json);

        var value = ImportCommand.ConvertJsonValue(document.RootElement, Column(sqlType));

        var expectedValue = sqlType is "decimal" or "numeric" or "money"
            ? 123.45m
            : expected;

        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("\"true\"", true)]
    [InlineData("\"false\"", false)]
    public void ConvertJsonValue_ConvertsBitValues(string json, bool expected)
    {
        using var document = JsonDocument.Parse(json);

        var value = ImportCommand.ConvertJsonValue(document.RootElement, Column("bit"));

        Assert.Equal(expected, value);
    }

    [Fact]
    public void ConvertJsonValue_ConvertsNullToDbNull()
    {
        using var document = JsonDocument.Parse("null");

        var value = ImportCommand.ConvertJsonValue(document.RootElement, Column("nvarchar"));

        Assert.Same(DBNull.Value, value);
    }

    [Fact]
    public void ConvertJsonValue_ConvertsBinaryFromBase64()
    {
        using var document = JsonDocument.Parse("\"AQID\"");

        var value = Assert.IsType<byte[]>(ImportCommand.ConvertJsonValue(document.RootElement, Column("varbinary")));

        Assert.Equal([1, 2, 3], value);
    }

    [Fact]
    public void ConvertJsonValue_WithInvalidBase64_ThrowsFormatException()
    {
        using var document = JsonDocument.Parse("\"not-base64\"");

        Assert.Throws<FormatException>(() => ImportCommand.ConvertJsonValue(document.RootElement, Column("varbinary")));
    }

    [Fact]
    public void ConvertJsonValue_ConvertsGuidAndTemporalTypes()
    {
        var guid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.Equal(guid, Convert("\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"", "uniqueidentifier"));
        Assert.Equal(
            DateTimeOffset.Parse("2026-06-27T16:30:01.0000000+02:00"),
            Convert("\"2026-06-27T16:30:01.0000000+02:00\"", "datetimeoffset"));
        Assert.Equal(
            DateTime.Parse("2026-06-27T14:30:01.0000000Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
            Convert("\"2026-06-27T14:30:01.0000000Z\"", "datetime2"));
        Assert.Equal(TimeSpan.Parse("01:02:03"), Convert("\"01:02:03\"", "time"));
    }

    [Theory]
    [InlineData("date", SqlDbType.Date)]
    [InlineData("datetime", SqlDbType.DateTime)]
    [InlineData("datetime2", SqlDbType.DateTime2)]
    [InlineData("datetimeoffset", SqlDbType.DateTimeOffset)]
    [InlineData("smalldatetime", SqlDbType.SmallDateTime)]
    [InlineData("time", SqlDbType.Time)]
    public void CreateParameter_UsesTheDestinationTemporalType(string sqlType, SqlDbType expected)
    {
        var value = sqlType == "datetimeoffset"
            ? (object)DateTimeOffset.MinValue
            : sqlType == "time"
                ? TimeSpan.Zero
                : DateTime.MinValue;

        var parameter = ImportCommand.CreateParameter("@value", value, Column(sqlType));

        Assert.Equal(expected, parameter.SqlDbType);
        Assert.Equal(value, parameter.Value);
    }

    [Fact]
    public void CreateParameter_ForDateTime2Minimum_DoesNotInferDateTime()
    {
        var value = Assert.IsType<DateTime>(Convert("\"0001-01-01T00:00:00.0000000\"", "datetime2"));

        var parameter = ImportCommand.CreateParameter("@value", value, Column("datetime2"));

        Assert.Equal(DateTime.MinValue, parameter.Value);
        Assert.Equal(SqlDbType.DateTime2, parameter.SqlDbType);
    }

    [Fact]
    public void ConvertJsonValue_UsesReasonableFallbacksForUnknownSqlTypes()
    {
        Assert.Equal("hello", Convert("\"hello\"", "nvarchar"));
        Assert.Equal(123L, Convert("123", "sql_variant"));
        Assert.Equal(123.45m, Convert("123.45", "sql_variant"));
        Assert.Equal(true, Convert("true", "sql_variant"));
        Assert.Equal("{\"nested\":true}", Convert("{\"nested\":true}", "json"));
    }

    [Theory]
    [InlineData(false, "int", true)]
    [InlineData(true, "int", false)]
    [InlineData(false, "rowversion", false)]
    [InlineData(false, "timestamp", false)]
    [InlineData(false, "ROWVERSION", false)]
    public void IsInsertable_RejectsComputedAndRowVersionColumns(bool isComputed, string sqlType, bool expected)
    {
        var column = new SqlColumnInfo("Column", sqlType, IsIdentity: false, isComputed, Ordinal: 1);

        Assert.Equal(expected, ImportCommand.IsInsertable(column));
    }

    [Fact]
    public void TryGetProperty_MatchesExportPropertyNamesCaseInsensitively()
    {
        using var document = JsonDocument.Parse("""
            { "Tables": [ { "Rows": [] } ] }
            """);

        Assert.True(ImportCommand.TryGetProperty(document.RootElement, "tables", out var tables));
        var table = Assert.Single(tables.EnumerateArray());

        Assert.True(ImportCommand.TryGetProperty(table, "rows", out var rows));
        Assert.Equal(JsonValueKind.Array, rows.ValueKind);
    }

    private static object Convert(string json, string sqlType)
    {
        using var document = JsonDocument.Parse(json);
        return ImportCommand.ConvertJsonValue(document.RootElement, Column(sqlType));
    }

    private static SqlColumnInfo Column(string sqlType) =>
        new("Column", sqlType, IsIdentity: false, IsComputed: false, Ordinal: 1);
}
