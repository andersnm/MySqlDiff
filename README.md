# MySqlDiff.CliTool - dotnet mysqldiff

**EXPERIMENTAL** Database diffing and migration tool for MySQL.

## Project format

The tool operates on a project directory structure like this:

```
/tables
  /*.sql
/triggers
  /*.sql
/procedures
  /*.sql
/seeds
  /*.sql
```

Users maintain the database schema and seeds by writing plain CREATE TABLE, CREATE PROCEDURE, CREATE TRIGGER, INSERT statements in SQL files under the `tables`, `triggers`, `procedures` and `seeds` directories.

The tool parses the database structure from these files, compares with a live database instance, and generates SQL to migrate the schema.

## Commands

```
dotnet mysqldiff diff --source ... --target ...
dotnet mysqldiff copy  --source ... --output ...
```

### diff

Compares two projects and generates SQL with the difference. Can be used to generate both up and down migration scripts by reversing the source and target arguments.

```
Options:
  --source <TYPE>                    fs, db or null
  --source-fs-path <PATH>            Project directory. Only used with --source fs
  --source-db-cs <CONNECTIONSTRING>  MySQL connection string. Only used with --source db
  --target <TYPE>                    fs, db or null
  --target-fs-path <PATH>            Project directory. Only used with --target fs
  --target-db-cs <CONNECTIONSTRING>  MySQL connection string. Only used with --target db
```

The 'null' source or target type specifies an empty project, which thusly generates SQL to create or delete the entire database.

### copy

Reads a project and saves it to the file syste. Can be used to export an initial project from a live database instance.

```
  --source <TYPE>                    fs, db or null
  --source-fs-path <PATH>            Project directory. Only used with --source fs
  --source-db-cs <CONNECTIONSTRING>  MySQL connection string. Only used with --source db
  --output <PATH>                    Where to save the output
```
