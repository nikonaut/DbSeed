namespace DbSeed.Tests;

public sealed class TableNameMatcherTests
{
    [Theory]
    [InlineData("Users")]
    [InlineData("users")]
    [InlineData("dbo.Users")]
    [InlineData("DBO.USERS")]
    [InlineData("[Users]")]
    public void Matches_WhenRequestedNameMatchesTable_ReturnsTrue(string requested)
    {
        var table = new SqlTableName("dbo", "Users");

        Assert.True(TableNameMatcher.Matches(requested, table));
    }

    [Theory]
    [InlineData("User")]
    [InlineData("audit.Users")]
    [InlineData("dbo.UserProfiles")]
    [InlineData("")]
    public void Matches_WhenRequestedNameDoesNotMatchTable_ReturnsFalse(string requested)
    {
        var table = new SqlTableName("dbo", "Users");

        Assert.False(TableNameMatcher.Matches(requested, table));
    }
}
