# DbSeed

DbSeed is a small .NET command-line tool for exporting SQL Server table data to JSON and importing that export back into a database. It is intended for moving seed data, test data, and small reference datasets between environments that already use `appsettings*.json` connection strings.

## Features

- Exports all user tables from a SQL Server database to a structured JSON document.
- Imports DbSeed JSON exports back into SQL Server.
- Reads connection strings from `appsettings*.json`.
- Supports table include and exclude filters.
- Preserves schema names, column metadata, identity values, binary values, GUIDs, and temporal values.
- Can delete existing rows before import with `--clean`.

## Requirements

- .NET 10 Runtime to run the published executable
- .NET 10 SDK to build from source
- SQL Server
- A project directory containing an `appsettings*.json` file with a `ConnectionStrings` section

Example `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Server=.;Database=MyApp;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

## Build

```powershell
dotnet build DbSeed.slnx
```

Run from source:

```powershell
dotnet run --project DbSeed -- --help
```

Publish a framework-dependent single-file Windows executable:

```powershell
dotnet publish DbSeed\DbSeed.csproj -p:PublishProfile=Executable
```

This produces `dbseed.exe` as a single-file executable. The target machine must have the .NET 10 Runtime installed.

## Usage

```text
dbseed export [options]
dbseed import [options] <file>
```

### Export

Export every user table and print JSON to the terminal:

```powershell
dbseed export --project C:\path\to\app
```

Export to a file:

```powershell
dbseed export --project C:\path\to\app --output seed-data.json
```

Use a specific appsettings file and connection string:

```powershell
dbseed export --project C:\path\to\app --appsettings appsettings.Development.json --connectionstring Default --output seed-data.json
```

Export only selected tables:

```powershell
dbseed export --include Users,dbo.Roles,audit.LogEntries --output seed-data.json
```

Exclude selected tables:

```powershell
dbseed export --exclude Logs,audit.LogEntries --output seed-data.json
```

### Import

Import an export file:

```powershell
dbseed import --project C:\path\to\app seed-data.json
```

Delete existing rows from each imported table before inserting rows:

```powershell
dbseed import --project C:\path\to\app --clean seed-data.json
```

Skip specific tables during import:

```powershell
dbseed import --exclude Logs,audit.LogEntries seed-data.json
```

## Options

| Option | Description |
| --- | --- |
| `-p`, `--project`, `--p` | Directory containing `appsettings*.json` files. Defaults to the current directory. |
| `-a`, `--appsettings` | Specific appsettings file to read. Relative paths are resolved from the project directory. |
| `-c`, `--connectionstring` | Connection string name from the `ConnectionStrings` section. Required when multiple connection strings exist. |
| `-i`, `--include` | Comma-separated list of tables to include when exporting. Supports `Table` or `schema.Table`. |
| `-e`, `--exclude` | Comma-separated list of tables to exclude. Supports `Table` or `schema.Table`. Exclude wins over include. |
| `-o`, `--output` | File to write export JSON to. If omitted, JSON is written to stdout. |
| `--clean`, `--clean-before-import` | Delete existing rows before importing each table. |
| `-h`, `--help` | Show CLI help. |

## Export Format

DbSeed writes JSON with this top-level shape:

```json
{
  "format": "DbSeed.export.v1",
  "generatedAt": "2026-06-27T14:30:01.0000000+00:00",
  "tables": [
    {
      "schema": "dbo",
      "name": "Users",
      "columns": [
        {
          "name": "Id",
          "dataType": "int",
          "isIdentity": true,
          "isComputed": false
        }
      ],
      "rows": [
        {
          "Id": 1,
          "Name": "Ada"
        }
      ]
    }
  ]
}
```

Binary values are exported as Base64 strings. `DateTime`, `DateTimeOffset`, `TimeSpan`, and `Guid` values are exported as round-trippable strings.

## Import Behavior

- Imports run inside a SQL transaction and roll back on failure.
- Computed columns and `rowversion`/`timestamp` columns are ignored during insert.
- Identity insert is enabled for a table when the export contains values for that table's identity column.
- `--clean` uses `DELETE FROM [schema].[table]` before importing each table.
- Table names are matched case-insensitively.

## Development

Run the test suite:

```powershell
dotnet test DbSeed.slnx
```

Project layout:

```text
DbSeed/          CLI source
DbSeed.Tests/    xUnit tests
DbSeed.slnx      Solution file
```
