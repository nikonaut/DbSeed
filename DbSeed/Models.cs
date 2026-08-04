namespace DbSeed;

internal sealed record CliOptions(
    CommandName Command,
    string? ProjectPath,
    string? AppsettingsFile,
    string? ConnectionStringName,
    IReadOnlySet<string> IncludeTables,
    IReadOnlySet<string> ExcludeTables,
    string? OutputFile,
    string? InputFile,
    bool CleanBeforeImport);

internal sealed record ParseResult(CliOptions? Options, string? Error, bool ShowHelp)
{
    public static ParseResult Success(CliOptions options) => new(options, null, false);

    public static ParseResult Failure(string error) => new(null, error, false);

    public static ParseResult Help() => new(null, null, true);
}

internal sealed record ResolvedConnection(string Name, string ConnectionString);

internal enum CommandName
{
    Export,
    Import
}

internal sealed record SqlTableName(string Schema, string Name)
{
    public string FullName => $"{Schema}.{Name}";

    public string QuotedName => $"{SqlServerSchema.QuoteIdentifier(Schema)}.{SqlServerSchema.QuoteIdentifier(Name)}";
}

internal sealed record SqlColumnInfo(string Name, string DataType, bool IsIdentity, bool IsComputed, int Ordinal);

internal sealed record ExportDocument
{
    public string Format { get; init; } = "DbSeed.export.v1";

    public ExportMetadata Metadata { get; init; } = new();

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public List<ExportTable> Tables { get; init; } = [];
}

internal sealed record ExportMetadata
{
    public string Description { get; init; } = "SQL Server table data exported by DbSeed for use with DbSeed.";

    public string CreatedBy { get; init; } = "DbSeed";

    public string ProjectUrl { get; init; } = "https://github.com/nikonaut/DbSeed";
}

internal sealed record ExportTable(
    string Schema,
    string Name,
    List<ExportColumn> Columns,
    List<Dictionary<string, object?>> Rows);

internal sealed record ExportColumn(string Name, string DataType, bool IsIdentity, bool IsComputed);
