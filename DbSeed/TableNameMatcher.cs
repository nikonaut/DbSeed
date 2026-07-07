namespace DbSeed;

internal static class TableNameMatcher
{
    public static bool Matches(string requested, SqlTableName table)
    {
        var clean = requested.Trim().Trim('[', ']');
        return clean.Contains('.', StringComparison.Ordinal)
            ? string.Equals(clean, table.FullName, StringComparison.OrdinalIgnoreCase)
            : string.Equals(clean, table.Name, StringComparison.OrdinalIgnoreCase);
    }
}
