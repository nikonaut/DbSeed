namespace DbSeed.Tests;

public sealed class ExportCommandTests
{
    [Fact]
    public void FilterTables_WithNoIncludeOrExclude_ReturnsAllTables()
    {
        var tables = Tables();

        var result = ExportCommand.FilterTables(tables, EmptySet(), EmptySet()).ToArray();

        Assert.Equal(tables, result);
    }

    [Fact]
    public void FilterTables_WithInclude_ReturnsOnlyMatchingTables()
    {
        var result = ExportCommand.FilterTables(
            Tables(),
            Set("users", "audit.LogEntries"),
            EmptySet()).ToArray();

        Assert.Equal([new SqlTableName("dbo", "Users"), new SqlTableName("audit", "LogEntries")], result);
    }

    [Fact]
    public void FilterTables_WithExclude_RemovesMatchingTables()
    {
        var result = ExportCommand.FilterTables(
            Tables(),
            EmptySet(),
            Set("logs", "audit.LogEntries")).ToArray();

        Assert.Equal([new SqlTableName("dbo", "Users")], result);
    }

    [Fact]
    public void FilterTables_ExcludeWinsWhenTableIsBothIncludedAndExcluded()
    {
        var result = ExportCommand.FilterTables(
            Tables(),
            Set("users", "logs"),
            Set("users")).ToArray();

        Assert.Equal([new SqlTableName("dbo", "Logs")], result);
    }

    [Fact]
    public void NormalizeValue_ConvertsSpecialClrValuesToJsonFriendlyValues()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var dateTime = new DateTime(2026, 6, 27, 14, 30, 1, DateTimeKind.Utc);
        var offset = new DateTimeOffset(2026, 6, 27, 16, 30, 1, TimeSpan.FromHours(2));
        var time = new TimeSpan(1, 2, 3);
        var guid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        Assert.Null(ExportCommand.NormalizeValue(null));
        Assert.Equal("AQID", ExportCommand.NormalizeValue(bytes));
        Assert.Equal("2026-06-27T14:30:01.0000000Z", ExportCommand.NormalizeValue(dateTime));
        Assert.Equal("2026-06-27T16:30:01.0000000+02:00", ExportCommand.NormalizeValue(offset));
        Assert.Equal("01:02:03", ExportCommand.NormalizeValue(time));
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", ExportCommand.NormalizeValue(guid));
        Assert.Equal(42, ExportCommand.NormalizeValue(42));
    }

    private static IReadOnlyList<SqlTableName> Tables() =>
    [
        new("dbo", "Users"),
        new("dbo", "Logs"),
        new("audit", "LogEntries")
    ];

    private static IReadOnlySet<string> Set(params string[] values) =>
        values.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> EmptySet() =>
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
