namespace DbSeed;

internal static class ArgumentParser
{
    public static ParseResult Parse(string[] args)
    {
        if (args.Any(a => a is "-h" or "--help"))
        {
            return ParseResult.Help();
        }

        string? command = null;
        string? project = null;
        string? appsettings = null;
        string? connectionString = null;
        string? include = null;
        string? exclude = null;
        string? output = null;
        var cleanBeforeImport = false;
        var positional = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            if (IsOption(arg))
            {
                if (IsFlagOption(arg))
                {
                    switch (arg)
                    {
                        case "--clean":
                        case "--clean-before-import":
                            cleanBeforeImport = true;
                            break;
                        default:
                            return ParseResult.Failure($"Unknown option '{arg}'.");
                    }

                    continue;
                }

                string value;
                try
                {
                    value = ReadOptionValue(args, ref index);
                }
                catch (DbSeedException ex)
                {
                    return ParseResult.Failure(ex.Message);
                }

                switch (arg)
                {
                    case "-p":
                    case "--p":
                    case "--project":
                        project = value;
                        break;
                    case "-a":
                    case "--appsettings":
                        appsettings = value;
                        break;
                    case "-c":
                    case "--connectionstring":
                        connectionString = value;
                        break;
                    case "-i":
                    case "--include":
                        include = value;
                        break;
                    case "-e":
                    case "--exclude":
                        exclude = value;
                        break;
                    case "-o":
                    case "--output":
                        output = value;
                        break;
                    default:
                        return ParseResult.Failure($"Unknown option '{arg}'.");
                }

                continue;
            }

            if (command is null)
            {
                command = arg;
            }
            else
            {
                positional.Add(arg);
            }
        }

        if (command is null)
        {
            return ParseResult.Failure("A command is required. Use 'export' or 'import'.");
        }

        if (!Enum.TryParse(command, ignoreCase: true, out CommandName commandName))
        {
            return ParseResult.Failure($"Unknown command '{command}'. Use 'export' or 'import'.");
        }

        if (commandName == CommandName.Export && positional.Count > 0)
        {
            return ParseResult.Failure("The export command does not accept positional arguments.");
        }

        if (commandName == CommandName.Import && positional.Count != 1)
        {
            return ParseResult.Failure("The import command requires exactly one file argument.");
        }

        return ParseResult.Success(new CliOptions(
            commandName,
            project,
            appsettings,
            connectionString,
            SplitCsv(include),
            SplitCsv(exclude),
            output,
            commandName == CommandName.Import ? positional[0] : null,
            cleanBeforeImport));
    }

    private static bool IsOption(string arg) => arg.StartsWith("-", StringComparison.Ordinal);

    private static bool IsFlagOption(string arg) =>
        arg is "--clean" or "--clean-before-import";

    private static string ReadOptionValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length || IsOption(args[index + 1]))
        {
            throw new DbSeedException($"Option '{args[index]}' requires a value.");
        }

        index++;
        return args[index];
    }

    private static IReadOnlySet<string> SplitCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
