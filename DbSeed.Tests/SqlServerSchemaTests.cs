namespace DbSeed.Tests;

public sealed class SqlServerSchemaTests
{
    [Theory]
    [InlineData("Users", "[Users]")]
    [InlineData("User Roles", "[User Roles]")]
    [InlineData("Weird]Name", "[Weird]]Name]")]
    public void QuoteIdentifier_EscapesSqlServerIdentifiers(string identifier, string expected)
    {
        Assert.Equal(expected, SqlServerSchema.QuoteIdentifier(identifier));
    }

    [Fact]
    public void SqlTableName_FormatsFullNameAndQuotedName()
    {
        var table = new SqlTableName("identity", "User]Roles");

        Assert.Equal("identity.User]Roles", table.FullName);
        Assert.Equal("[identity].[User]]Roles]", table.QuotedName);
    }
}
