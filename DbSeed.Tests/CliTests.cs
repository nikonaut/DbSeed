namespace DbSeed.Tests;

public sealed class CliTests
{
    [Fact]
    public async Task RunAsync_WithHelp_PrintsHelpAndReturnsSuccess()
    {
        using var console = ConsoleCapture.Start();

        var exitCode = await Cli.RunAsync(["--help"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("dbseed export [options]", console.Out);
        Assert.Equal("", console.Error);
    }

    [Fact]
    public async Task RunAsync_WithNoArguments_PrintsCommandRequiredError()
    {
        using var console = ConsoleCapture.Start();

        var exitCode = await Cli.RunAsync([]);

        Assert.Equal(1, exitCode);
        Assert.Equal("", console.Out);
        Assert.Contains("dbseed: A command is required. Use 'export' or 'import'.", console.Error);
    }

    [Fact]
    public async Task RunAsync_WithUnknownOption_PrintsErrorAndReturnsFailure()
    {
        using var console = ConsoleCapture.Start();

        var exitCode = await Cli.RunAsync(["export", "--bogus", "value"]);

        Assert.Equal(1, exitCode);
        Assert.Equal("", console.Out);
        Assert.Contains("dbseed: Unknown option '--bogus'.", console.Error);
    }

    [Fact]
    public async Task RunAsync_WithMissingProjectDirectory_PrintsErrorAndReturnsFailure()
    {
        using var console = ConsoleCapture.Start();
        using var directory = TemporaryDirectory.Create();
        var missingDirectory = directory.File("missing-project");

        var exitCode = await Cli.RunAsync(["export", "--project", missingDirectory]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Project directory", console.Error);
        Assert.Contains("does not exist", console.Error);
    }

    [Fact]
    public async Task RunAsync_WithNoAppsettingsInCurrentDirectory_PrintsErrorAndReturnsFailure()
    {
        using var directory = TemporaryDirectory.Create();
        using var currentDirectory = CurrentDirectory.ChangeTo(directory.Path);
        using var console = ConsoleCapture.Start();

        var exitCode = await Cli.RunAsync(["export"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("No appsettings file with connection string data was found", console.Error);
        Assert.Contains("could not execute", console.Error);
    }
}
