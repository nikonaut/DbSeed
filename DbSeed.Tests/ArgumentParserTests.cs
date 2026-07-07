namespace DbSeed.Tests;

public sealed class ArgumentParserTests
{
    [Fact]
    public void Parse_WithNoArguments_ReturnsCommandRequiredError()
    {
        var result = ArgumentParser.Parse([]);

        Assert.False(result.ShowHelp);
        Assert.Null(result.Options);
        Assert.Equal("A command is required. Use 'export' or 'import'.", result.Error);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Parse_WithHelpArgument_ShowsHelp(string helpArgument)
    {
        var result = ArgumentParser.Parse([helpArgument]);

        Assert.True(result.ShowHelp);
    }

    [Fact]
    public void Parse_ExportWithAllOptions_MapsValues()
    {
        var result = ArgumentParser.Parse([
            "export",
            "--project", "MyApp\\MyProject",
            "--appsettings", "appsettings.Development.json",
            "--connectionstring", "testdb",
            "--include", "users, dbo.Posts,users",
            "--exclude", "logs, audit",
            "--output", "C:\\Temp\\output.json"
        ]);

        var options = AssertSuccess(result);
        Assert.Equal(CommandName.Export, options.Command);
        Assert.Equal("MyApp\\MyProject", options.ProjectPath);
        Assert.Equal("appsettings.Development.json", options.AppsettingsFile);
        Assert.Equal("testdb", options.ConnectionStringName);
        Assert.Equal("C:\\Temp\\output.json", options.OutputFile);
        Assert.Null(options.InputFile);
        Assert.False(options.CleanBeforeImport);
        Assert.Equal(["dbo.Posts", "users"], options.IncludeTables.Order(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(["audit", "logs"], options.ExcludeTables.Order(StringComparer.OrdinalIgnoreCase));
        Assert.Contains("USERS", options.IncludeTables);
    }

    [Fact]
    public void Parse_ExportOptionsWithoutCommand_ReturnsCommandRequiredError()
    {
        var result = ArgumentParser.Parse([
            "--project", "MyApp\\MyProject",
            "--output", "export.json"
        ]);

        Assert.False(result.ShowHelp);
        Assert.Null(result.Options);
        Assert.Equal("A command is required. Use 'export' or 'import'.", result.Error);
    }

    [Fact]
    public void Parse_ExportSupportsShortAliases()
    {
        var result = ArgumentParser.Parse([
            "export",
            "-p", "Project",
            "-a", "appsettings.local.json",
            "-c", "Default",
            "-i", "users",
            "-e", "logs",
            "-o", "export.json"
        ]);

        var options = AssertSuccess(result);
        Assert.Equal("Project", options.ProjectPath);
        Assert.Equal("appsettings.local.json", options.AppsettingsFile);
        Assert.Equal("Default", options.ConnectionStringName);
        Assert.Contains("users", options.IncludeTables);
        Assert.Contains("logs", options.ExcludeTables);
        Assert.Equal("export.json", options.OutputFile);
    }

    [Fact]
    public void Parse_ProjectSupportsDocumentedLongPAlias()
    {
        var result = ArgumentParser.Parse(["export", "--p", "Project"]);

        var options = AssertSuccess(result);
        Assert.Equal("Project", options.ProjectPath);
    }

    [Fact]
    public void Parse_ImportWithInputExcludeAndClean_MapsValues()
    {
        var result = ArgumentParser.Parse(["import", "--exclude", "userauths,passwordhashes", "--clean", "output.json"]);

        var options = AssertSuccess(result);
        Assert.Equal(CommandName.Import, options.Command);
        Assert.Equal("output.json", options.InputFile);
        Assert.Contains("userauths", options.ExcludeTables);
        Assert.Contains("PASSWORDHASHES", options.ExcludeTables);
        Assert.Empty(options.IncludeTables);
        Assert.Null(options.OutputFile);
        Assert.True(options.CleanBeforeImport);
    }

    [Fact]
    public void Parse_ImportWithCleanBeforeImportAlias_MapsValue()
    {
        var result = ArgumentParser.Parse(["import", "--clean-before-import", "output.json"]);

        var options = AssertSuccess(result);
        Assert.True(options.CleanBeforeImport);
    }

    [Theory]
    [InlineData("export", "--unknown", "value", "Unknown option '--unknown'.")]
    [InlineData("export", "--project", "Option '--project' requires a value.")]
    [InlineData("export", "-o", "-x", "Option '-o' requires a value.")]
    [InlineData("dump", "Unknown command 'dump'. Use 'export' or 'import'.")]
    [InlineData("export", "extra.json", "The export command does not accept positional arguments.")]
    [InlineData("import", "The import command requires exactly one file argument.")]
    [InlineData("import", "one.json", "two.json", "The import command requires exactly one file argument.")]
    public void Parse_InvalidArguments_ReturnsExpectedError(params string[] argsAndError)
    {
        var expectedError = argsAndError[^1];
        var args = argsAndError[..^1];

        var result = ArgumentParser.Parse(args);

        Assert.False(result.ShowHelp);
        Assert.Null(result.Options);
        Assert.Equal(expectedError, result.Error);
    }

    private static CliOptions AssertSuccess(ParseResult result)
    {
        Assert.False(result.ShowHelp);
        Assert.Null(result.Error);
        Assert.NotNull(result.Options);
        return result.Options;
    }
}
