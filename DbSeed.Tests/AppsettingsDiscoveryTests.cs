using System.Text.Json;

namespace DbSeed.Tests;

public sealed class AppsettingsDiscoveryTests
{
    [Fact]
    public void ResolveAppsettings_WithoutExplicitFile_ReturnsSingleAppsettingsWithConnectionStrings()
    {
        using var directory = TemporaryDirectory.Create();
        directory.Write("appsettings.json", """
            {
              "ConnectionStrings": {
                "Default": "Server=.;Database=App;Trusted_Connection=True;"
              }
            }
            """);
        directory.Write("appsettings.empty.json", "{}");

        var result = AppsettingsDiscovery.ResolveAppsettings(directory.Path, appsettingsFile: null);

        Assert.Equal(directory.File("appsettings.json"), result);
    }

    [Fact]
    public void ResolveAppsettings_WithRelativeExplicitFile_ReturnsResolvedPath()
    {
        using var directory = TemporaryDirectory.Create();
        directory.Write("appsettings.local.json", """
            {
              "ConnectionStrings": {
                "Default": "Server=.;Database=Local;Trusted_Connection=True;"
              }
            }
            """);

        var result = AppsettingsDiscovery.ResolveAppsettings(directory.Path, "appsettings.local.json");

        Assert.Equal(directory.File("appsettings.local.json"), result);
    }

    [Fact]
    public void ResolveAppsettings_WithAbsoluteExplicitFile_ReturnsAbsolutePath()
    {
        using var directory = TemporaryDirectory.Create();
        var appsettings = directory.Write("custom.json", "{}");

        var result = AppsettingsDiscovery.ResolveAppsettings(directory.Path, appsettings);

        Assert.Equal(appsettings, result);
    }

    [Fact]
    public void ResolveAppsettings_WithMissingExplicitFile_ThrowsHelpfulError()
    {
        using var directory = TemporaryDirectory.Create();
        var missing = directory.File("missing.json");

        var exception = Assert.Throws<DbSeedException>(() =>
            AppsettingsDiscovery.ResolveAppsettings(directory.Path, "missing.json"));

        Assert.Contains(missing, exception.Message);
        Assert.Contains("does not exist", exception.Message);
    }

    [Fact]
    public void ResolveAppsettings_WithNoConnectionStringFiles_ThrowsHelpfulError()
    {
        using var directory = TemporaryDirectory.Create();
        directory.Write("appsettings.json", "{\"Logging\":{}}");

        var exception = Assert.Throws<DbSeedException>(() =>
            AppsettingsDiscovery.ResolveAppsettings(directory.Path, appsettingsFile: null));

        Assert.Contains("No appsettings file with connection string data was found", exception.Message);
        Assert.Contains(directory.Path, exception.Message);
    }

    [Fact]
    public void ResolveAppsettings_WithMultipleConnectionStringFiles_ThrowsHelpfulError()
    {
        using var directory = TemporaryDirectory.Create();
        directory.Write("appsettings.json", "{\"ConnectionStrings\":{\"Default\":\"one\"}}");
        directory.Write("appsettings.development.json", "{\"ConnectionStrings\":{\"Default\":\"two\"}}");

        var exception = Assert.Throws<DbSeedException>(() =>
            AppsettingsDiscovery.ResolveAppsettings(directory.Path, appsettingsFile: null));

        Assert.Contains("Multiple appsettings files", exception.Message);
        Assert.Contains("appsettings.json", exception.Message);
        Assert.Contains("appsettings.development.json", exception.Message);
        Assert.Contains("--appsettings", exception.Message);
    }

    [Fact]
    public void ReadConnectionStrings_ReadsCaseInsensitiveSectionWithCommentsAndTrailingComma()
    {
        using var directory = TemporaryDirectory.Create();
        var appsettings = directory.Write("appsettings.json", """
            {
              // JSON comments are accepted by .NET appsettings files.
              "connectionstrings": {
                "Default": "Server=.;Database=App;",
                "Reporting": "Server=.;Database=Reporting;",
              },
            }
            """);

        var values = AppsettingsDiscovery.ReadConnectionStrings(appsettings);

        Assert.Equal("Server=.;Database=App;", values["default"]);
        Assert.Equal("Server=.;Database=Reporting;", values["REPORTING"]);
    }

    [Fact]
    public void ReadConnectionStrings_IgnoresEmptyNullAndNonStringValues()
    {
        using var directory = TemporaryDirectory.Create();
        var appsettings = directory.Write("appsettings.json", """
            {
              "ConnectionStrings": {
                "Default": "Server=.;Database=App;",
                "Empty": "",
                "Whitespace": "   ",
                "NullValue": null,
                "ObjectValue": { "Server": "." }
              }
            }
            """);

        var values = AppsettingsDiscovery.ReadConnectionStrings(appsettings);

        var only = Assert.Single(values);
        Assert.Equal("Default", only.Key);
        Assert.Equal("Server=.;Database=App;", only.Value);
    }

    [Fact]
    public void ReadConnectionStrings_WithInvalidJson_ThrowsJsonException()
    {
        using var directory = TemporaryDirectory.Create();
        var appsettings = directory.Write("appsettings.json", "{ invalid json");

        Assert.ThrowsAny<JsonException>(() => AppsettingsDiscovery.ReadConnectionStrings(appsettings));
    }
}
