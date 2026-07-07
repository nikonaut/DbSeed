using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace DbSeed;

internal static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var parseResult = ArgumentParser.Parse(args);
            if (parseResult.ShowHelp)
            {
                Console.WriteLine(HelpText);
                return 0;
            }

            if (parseResult.Error is not null)
            {
                return Fail(parseResult.Error);
            }

            var options = parseResult.Options!;
            var projectDirectory = ResolveProjectDirectory(options.ProjectPath);
            var connection = ResolveConnection(projectDirectory, options);

            return options.Command switch
            {
                CommandName.Export => await ExportCommand.ExecuteAsync(connection.ConnectionString, options),
                CommandName.Import => await ImportCommand.ExecuteAsync(connection.ConnectionString, options),
                _ => Fail("Unknown command. Use 'export' or 'import'.")
            };
        }
        catch (DbSeedException ex)
        {
            return Fail(ex.Message);
        }
        catch (SqlException ex)
        {
            return Fail($"Database error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Fail($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Fail($"Unexpected error: {ex.Message}");
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine($"dbseed: {message}");
        return 1;
    }

    private static string ResolveProjectDirectory(string? projectPath)
    {
        var baseDirectory = Directory.GetCurrentDirectory();
        var directory = string.IsNullOrWhiteSpace(projectPath)
            ? baseDirectory
            : Path.GetFullPath(projectPath, baseDirectory);

        if (!Directory.Exists(directory))
        {
            throw new DbSeedException($"Project directory '{directory}' does not exist.");
        }

        return directory;
    }

    private static ResolvedConnection ResolveConnection(string projectDirectory, CliOptions options)
    {
        var appsettings = AppsettingsDiscovery.ResolveAppsettings(projectDirectory, options.AppsettingsFile);
        var connectionStrings = AppsettingsDiscovery.ReadConnectionStrings(appsettings);

        if (connectionStrings.Count == 0)
        {
            throw new DbSeedException($"No ConnectionStrings section with values was found in '{appsettings}'.");
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionStringName))
        {
            if (connectionStrings.Count > 1)
            {
                var names = string.Join(", ", connectionStrings.Keys.Order(StringComparer.OrdinalIgnoreCase));
                throw new DbSeedException(
                    $"Multiple connection strings are available ({names}). Specify one with -c {{connectionstring-name}} or --connectionstring {{connectionstring-name}}.");
            }

            var only = connectionStrings.Single();
            return new ResolvedConnection(only.Key, only.Value);
        }

        if (!connectionStrings.TryGetValue(options.ConnectionStringName, out var connectionString))
        {
            var names = string.Join(", ", connectionStrings.Keys.Order(StringComparer.OrdinalIgnoreCase));
            throw new DbSeedException(
                $"Connection string '{options.ConnectionStringName}' was not found. Available connection strings: {names}.");
        }

        return new ResolvedConnection(options.ConnectionStringName, connectionString);
    }

    private const string HelpText = """
        Usage:
          dbseed export [options]
          dbseed import [options] <file>

        Commands:
          export                 Export database table data as JSON.
          import                 Import a DbSeed JSON export into the database.

        Options:
          -p, --project <dir>           Directory containing appsettings*.json files.
              --p <dir>                 Alias for --project.
          -a, --appsettings <file>      Appsettings file to use.
          -c, --connectionstring <name> ConnectionStrings entry name to use.
          -i, --include <tables>        Comma-separated tables to include when exporting.
          -e, --exclude <tables>        Comma-separated tables to exclude.
          -o, --output <file>           Write export JSON to a file instead of the terminal.
              --clean                   Delete existing rows before importing each table.
              --clean-before-import     Alias for --clean.
          -h, --help                    Show help.
        """;
}
