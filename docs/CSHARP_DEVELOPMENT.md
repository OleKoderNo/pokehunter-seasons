# C# Development Setup

This guide explains how to edit, build, and test PokéHunter's C# code
using VS Code and Streamer.bot.

Reusable code is compiled into shared libraries. Streamer.bot action
source files are pasted into Execute C# Code sub-actions.

Building the project, installing its libraries, and updating pasted
actions are separate steps.

## Requirements

- Windows x64.
- VS Code with Microsoft's C# extension.
- .NET SDK 10.
- Streamer.bot.
- SQLite libraries configured using [SQLite Setup](SQLITE_SETUP.md).

The setup has been verified with SDK 10.0.401 and Streamer.bot 1.0.7.

The C# projects target .NET Framework 4.8.1 and use C# 7.3.
Installing the .NET 10 SDK does not change the runtime used by
Streamer.bot.

## 1. Configure your installation path

Inside `streamerbot`, copy:

```text
StreamerBot.local.props.example
```

Rename the copy to:

```text
StreamerBot.local.props
```

Edit `StreamerBotPath` to point to the folder containing
`Streamer.bot.exe`:

```xml
<Project>
  <PropertyGroup>
    <StreamerBotPath>C:\Tools\Streamer.bot</StreamerBotPath>
  </PropertyGroup>
</Project>
```

Replace the example path with your actual installation path.
Do not add quotation marks around it.

The local file is ignored by Git. The example file is committed so
other developers can configure their own installation.

The C# projects use this setting to locate the required libraries
from your Streamer.bot installation.

## 2. Understand the projects

A `.csproj` file contains build instructions and dependency references.
A generated `.dll` contains compiled code.

### Streamer.bot action project

```text
streamerbot/PokeHunter.StreamerBot.csproj
```

This project provides API references and compilation checks for the
Streamer.bot action source files.

It references both shared-library projects:

- `configuration/PokeHunter.Core.csproj`
- `storage/PokeHunter.Storage.csproj`

Their source directories are excluded from direct compilation by the
action project. This prevents the same classes from being compiled
into multiple assemblies.

Its generated `PokeHunter.StreamerBot.dll` is a development build
artifact. Do not install it into Streamer.bot.

Action source files are copied into Execute C# Code sub-actions.

### Configuration library

```text
streamerbot/configuration/PokeHunter.Core.csproj
```

This project compiles configuration loading, validation, time-zone
support, and season and event selection into:

```text
PokeHunter.Core.dll
```

The library uses:

- Streamer.bot's `Newtonsoft.Json.dll` for JSON handling.
- TimeZoneConverter for configured time-zone resolution.

Install `PokeHunter.Core.dll` and `TimeZoneConverter.dll` into
Streamer.bot so actions can use this functionality.

### Storage library

```text
streamerbot/storage/PokeHunter.Storage.csproj
```

This project compiles database initialization, migrations, storage
models, and repository code into:

```text
PokeHunter.Storage.dll
```

It uses `System.Data.SQLite` and the .NET Framework `System.Data`
assembly.

Migration SQL files are embedded into this library. Updating migration
source therefore requires rebuilding and installing the updated DLL.

See [Database Design](DATABASE.md) for the schema and migration rules.

### Standalone configuration-check project

```text
tests/configuration/ConfigurationChecks.csproj
```

This project builds a small executable that checks configuration
behaviour outside Streamer.bot.

It references `PokeHunter.Core`, so the checks exercise the same
configuration implementation used by the Streamer.bot actions.

The standalone checks cover game settings, seasons, events, selection
boundaries, and time-zone behaviour.

## 3. Build the C# projects

Run commands from the repository root: the folder containing
`config`, `docs`, `streamerbot`, and `tests`.

Build the action project and both referenced libraries:

```powershell
dotnet build streamerbot/PokeHunter.StreamerBot.csproj
```

The first build restores required NuGet packages, including the
.NET Framework reference assemblies.

The main build outputs are:

| File                                                               | Purpose                                                   |
| ------------------------------------------------------------------ | --------------------------------------------------------- |
| `streamerbot/bin/Debug/net481/PokeHunter.StreamerBot.dll`          | Local compilation artifact for action source.             |
| `streamerbot/configuration/bin/Debug/net481/PokeHunter.Core.dll`   | Configuration library installed into Streamer.bot.        |
| `streamerbot/configuration/bin/Debug/net481/TimeZoneConverter.dll` | Dependency installed alongside the configuration library. |
| `streamerbot/storage/bin/Debug/net481/PokeHunter.Storage.dll`      | Storage library installed into Streamer.bot.              |

Building checks compilation and references. It does not execute
actions, modify player data, or install DLLs into Streamer.bot.

Generated `bin` and `obj` directories should remain ignored by Git.
Commit source files, project files, and migration SQL files instead
of generated build output.

The npm checks cover TypeScript tooling and supported formatting.
They do not replace the C# build or the C# runtime checks.

## 4. Understand the editor declarations

The Streamer.bot action project defines `EXTERNAL_EDITOR` during
local compilation.

Action files use this to select a unique class name and explicitly
inherit Streamer.bot's base class:

```csharp
#if EXTERNAL_EDITOR
public class SQLitePersistenceCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
```

Each action file must use its own unique editor class name.

The `Execute` method also has separate declarations:

```csharp
#if EXTERNAL_EDITOR
public new bool Execute()
#else
public bool Execute()
#endif
```

The `new` keyword explicitly hides the inherited method in the editor
build, avoiding warning CS0114.

Inside Streamer.bot, `EXTERNAL_EDITOR` is not defined. The action
therefore uses `CPHInline` and `public bool Execute()`.

Copy these conditional declarations along with the rest of the action
source. Do not define `EXTERNAL_EDITOR` inside Streamer.bot.

Reusable configuration and storage classes do not need these
conditional declarations.

## 5. Format C# files

Prettier handles the project's supported TypeScript, JSON, and Markdown
files. Microsoft's C# extension handles C# formatting.

In VS Code:

1. Open a `.cs` file.
2. Select **Format Document With...** from the right-click menu.
3. Select **Configure Default Formatter**.
4. Choose Microsoft's **C#** formatter.

To format C# files automatically when saving, merge this property into
your existing VS Code settings object:

```json
"[csharp]": {
  "editor.defaultFormatter": "ms-dotnettools.csharp",
  "editor.formatOnSave": true
}
```

Keep your other settings and add a separating comma where necessary.

You can also format the open file manually with **Shift + Alt + F**.

Passing `npm run format:check` does not mean that C# files were formatted
or that C# code compiled successfully.

## 6. Install or update shared libraries

After a successful build:

1. Exit Streamer.bot completely, including any system-tray instance.
2. Open `streamerbot/configuration/bin/Debug/net481/`.
3. Copy `PokeHunter.Core.dll` and `TimeZoneConverter.dll`.
4. Open `streamerbot/storage/bin/Debug/net481/`.
5. Copy `PokeHunter.Storage.dll`.
6. Place these files in the `dlls` directory inside your Streamer.bot
   installation, replacing the previous copies when updating.
7. Reopen Streamer.bot.

For an installation at `C:\Tools\Streamer.bot`, the installed files are:

```text
C:\Tools\Streamer.bot\dlls\PokeHunter.Core.dll
C:\Tools\Streamer.bot\dlls\TimeZoneConverter.dll
C:\Tools\Streamer.bot\dlls\PokeHunter.Storage.dll
```

TimeZoneConverter is restored through the configuration project's
NuGet package reference. It does not need to be downloaded separately.

SQLite has additional installation requirements. Follow
[SQLite Setup](SQLITE_SETUP.md) for its managed and native libraries.

Building does not automatically update installed DLLs.

If copying reports that a DLL is in use, confirm that Streamer.bot
has fully exited. PowerShell's `-Force` option does not bypass a file
lock held by another process.

Do not install `.csproj` files or `PokeHunter.StreamerBot.dll`.

## 7. Run an action in Streamer.bot

For each manual action:

1. Edit and save its source file in VS Code.
2. Build the Streamer.bot action project.
3. Install updated shared libraries if their code changed.
4. Create or open the corresponding Streamer.bot action.
5. Add or open its Execute C# Code sub-action.
6. Paste the complete action source.
7. Configure that sub-action's required assembly references.
8. Save and compile.
9. Run the action and inspect its log output.

If the source contains a `ProjectFolder` setting, set it to the
absolute path of your repository root before copying the source.

For example:

```csharp
private const string ProjectFolder =
    @"C:\Projects\pokehunter-seasons";
```

Use the existing declaration in the file and replace its path.
Do not add a second declaration.

Editing a source file does not automatically update a pasted action.

The `.csproj` references configure local compilation. They do not
automatically configure assembly references in Streamer.bot.

Copying a DLL into the installation directory and adding it as an
action reference are separate steps.

## Configuration implementation

Configuration code is in `streamerbot/configuration/` and is compiled
into `PokeHunter.Core.dll`.

See [Configuration Guide](CONFIGURATION.md) for editable settings.

### Shared support

- `RequiredConfigContractResolver.cs` maps property names to camelCase
  and requires configuration properties to be present and non-null.
- `ExplicitOffsetDateTimeConverter.cs` reads timestamps with an
  explicit UTC offset or `Z`.
- `ConfigurationTimeZone.cs` resolves configured time-zone identifiers
  and reports resolution failures as configuration errors.

Unknown JSON properties are rejected to help catch misspelled settings.
Invalid settings are reported instead of silently replaced with defaults.

### Game configuration

- `GameConfig.cs` describes `config/game.json`.
- `GameConfigLoader.cs` reads the JSON and calls the validator.
- `GameConfigValidator.cs` checks the supported schema version and
  numerical rules.

Reading the reward cost or cooldown from configuration does not
automatically configure the Twitch reward or enforce a cooldown.

### Season configuration

- `SeasonsConfig.cs` describes season configuration and definitions.
- `SeasonsConfigLoader.cs` reads and validates `config/seasons.json`.
- `SeasonsConfigValidator.cs` checks values and schedule continuity.
- `SeasonSelector.cs` selects the season containing a supplied instant.

Season validation requires:

- Schema version `1`.
- A resolvable time-zone identifier.
- At least one season.
- Nonempty IDs and names without surrounding whitespace.
- Unique season IDs, compared without regard to capitalization.
- A supplied starting timestamp that is not the default value.
- An exclusive ending instant later than the start.
- Nonempty generation lists containing distinct positive integers.
- A continuous schedule without overlaps or gaps.

Each season after the first must start at exactly the preceding
season's exclusive end.

Annual durations and cumulative generation unlocks are not hardcoded
validation requirements.

Generation availability against the Pokémon catalogue is not checked yet.

### Event configuration

- `EventsConfig.cs` describes event configuration and definitions.
- `EventsConfigLoader.cs` reads and validates `config/events.json`.
- `EventsConfigValidator.cs` checks event values and scheduling rules.
- `EventSelector.cs` selects the enabled event containing a supplied instant.

Unlike seasons, events may have gaps, and an empty event schedule
is allowed.

Enabled events must not overlap. Disabled events are ignored during
selection and overlap checking, but their configuration values must
still be valid.

Event configuration includes category weights, generation restrictions,
and inclusion selectors for types, evolution families, forms, and costumes.

Loading these selectors does not yet resolve them into a Pokémon pool.

### Timestamps and selection boundaries

Season and event boundaries use `DateTimeOffset`.

Timestamps must include seconds and an explicit offset or `Z`.
Up to seven fractional-second digits are supported.

These timestamps represent the same instant:

```text
2027-01-01T00:00:00+01:00
2026-12-31T23:00:00Z
```

This timestamp is rejected because it has no offset:

```text
2027-01-01T00:00:00
```

Both selectors use inclusive starts and exclusive ends:

```text
startsAt <= instant < endsAtExclusive
```

At a shared season boundary, the previous season ends and the next
season becomes active immediately.

The season selector returns `null` before the first configured season
and at or after the final configured end. It does not reuse an expired
season or create a future one.

The event selector returns `null` when no enabled event covers the
supplied instant.

Selection compares instants, accounting for their explicit offsets.
Definition order does not determine the selected season or event.

### Time-zone behaviour

TimeZoneConverter resolves supported IANA and Windows time-zone identifiers.

The configured zone controls local-time display. Explicit timestamp
offsets define the actual season and event boundaries.

The validator does not require a timestamp's written offset to match
the named zone's local offset. Equivalent UTC or explicit-offset
representations remain valid.

Changing `timeZone` does not move a boundary. Edit the boundary
timestamp to change when it occurs.

### Current limits

Game, season, and event configuration loading and selection are
verified through standalone checks and a manual Streamer.bot action.

Configuration loading is not yet connected to a live catching action.
Selecting a season does not create or archive collection records.

The overlay configuration loader is not implemented yet.

## Run the standalone configuration checks

From the repository root, run:

```powershell
dotnet build tests/configuration/ConfigurationChecks.csproj

if ($LASTEXITCODE -eq 0) {
    & ".\tests\configuration\bin\Debug\net481\ConfigurationChecks.exe" ".\config\game.json" ".\config\seasons.json" ".\config\events.json"
}
```

Building this project also builds its referenced configuration library.

The condition runs the executable only when the build succeeds.
This prevents accidentally running an older executable after a
failed build.

The executable requires three arguments, in this order:

1. The path to `game.json`.
2. The path to `seasons.json`.
3. The path to `events.json`.

### Check organization

| File                           | Responsibility                                                                          |
| ------------------------------ | --------------------------------------------------------------------------------------- |
| `Program.cs`                   | Reads arguments, runs check groups, reports results, and manages temporary files.       |
| `GameConfigurationChecks.cs`   | Checks real game settings and rejection of a zero milestone size.                       |
| `SeasonConfigurationChecks.cs` | Checks season loading, adjoining timestamps, overlaps, and explicit offsets.            |
| `SeasonSelectionChecks.cs`     | Checks boundaries, equivalent instants, definition order, and gap rejection.            |
| `TimeZoneChecks.cs`            | Checks time-zone resolution, winter and summer conversions, and invalid-zone rejection. |
| `EventTestFixtures.cs`         | Creates fresh event configurations for independent checks.                              |
| `EventConfigurationChecks.cs`  | Checks event loading, selection, disabled events, gaps, and selected invalid settings.  |
| `CheckAssert.cs`               | Verifies that invalid configurations fail for the expected reason.                      |
| `TestJsonFiles.cs`             | Writes temporary JSON fixtures.                                                         |

These files belong to the standalone executable. They are not pasted
into Streamer.bot or compiled into `PokeHunter.Core.dll`.

The standalone project references the configuration library instead
of compiling another copy of its source files.

Its Newtonsoft.Json reference uses `Private=true` so the executable
receives a local copy of that dependency.

### What the checks cover

The checks exercise:

- Loading the supplied game, season, and event configurations.
- Rejecting a zero game collection-milestone size.
- Season starts, transitions, and exclusive ends.
- Equivalent timestamps with different offsets.
- Selection independent of definition order.
- Rejection of season overlaps and gaps.
- Rejection of timestamps without explicit offsets.
- Oslo winter and summer time-zone conversions.
- Rejection of an unknown time zone.
- Event starts and exclusive ends.
- Ignoring disabled events during selection and overlap checks.
- Accepting event gaps and empty event schedules.
- Rejecting overlapping enabled events.
- Rejecting a zero event category weight.
- Rejecting unsupported Pokémon types.
- Rejecting a missing Legendary restriction setting.

These checks cover specific behaviours, not every validation rule
or future encounter rule.

### Results and temporary files

A successful run ends with:

```text
All configuration checks passed.
```

Reported settings and schedule counts reflect the supplied configuration.

The executable returns exit code `0` on success and `1` on failure.

Invalid examples are written to a temporary directory. The supplied
configuration files are not modified.

Fixed fixtures keep boundary checks independent of today's date and
the creator's actual schedule.

Cleanup is attempted whether the checks pass or fail. A cleanup
warning does not replace the original check result.

### Adding a check

1. Choose the appropriate check class.
2. Add a method named for the behaviour being checked.
3. Call it from that class's `Run` method.
4. Start with a fresh valid example and change only what the scenario needs.
5. Use `TestJsonFiles.Write` when the loader needs a temporary file.
6. For expected validation failures, use `CheckAssert.Rejected` with
   message fragments identifying the intended rejection reason.
7. Print a PASS message only after the assertions succeed.
8. Build and run the complete executable.

`CheckAssert.Rejected` requires `InvalidDataException`.
An unrelated exception, an unexpected rejection reason, or invalid
input being accepted causes the check to fail.

For a new check group, call its `Run` method from `Program`.
Creating a method or file alone does not execute a check.

### Preserve regression checks

Keep existing checks active when adding new behaviour.

Moving a check into another file should preserve its inputs,
assertions, and expected results.

Change or remove an existing check only when its expected behaviour
has deliberately changed, and explain that change in the commit.

These are explicitly called standalone checks, not an automatically
discovered test suite.

## Manual configuration check in Streamer.bot

The action source is:

```text
streamerbot/tests/GameConfigurationCheck.cs
```

Despite its original name, this action checks game, season, and
event configuration.

### Setup

1. Build and install the configuration library and its dependency.
2. Create an action named `PokéHunter — Game Configuration Check`.
3. Add an Execute C# Code sub-action.
4. Paste the complete `GameConfigurationCheck.cs` source.
5. Set `ProjectFolder` to the absolute repository path.
6. Open the References tab and add the required assemblies.
7. Save and compile.
8. Run the action manually.

Required assembly references:

| Assembly                | Location inside Streamer.bot |
| ----------------------- | ---------------------------- |
| `PokeHunter.Core.dll`   | `dlls` directory.            |
| `TimeZoneConverter.dll` | `dlls` directory.            |
| `Newtonsoft.Json.dll`   | Installation root.           |

### Behaviour and expected results

The action:

1. Loads and validates all three configuration files.
2. Captures the current UTC instant once.
3. Uses that instant to select the active season and event.
4. Logs the instant in UTC and each configured local time zone.
5. Logs game settings and configuration paths.
6. Logs the active season and its unlocked generations.
7. Logs the active event's weight, restrictions, and selector counts.

Selector counts are not Pokémon counts. One evolution-family selector
may eventually match several Pokémon.

No active event is a normal result.

No active season produces a warning and returns `false`.
Loading or validation errors are logged and return `false`.

With the current default game settings, the output includes:

```text
[PokéHunter] Game configuration check passed.
[PokéHunter] Reward cost: 500 | Cooldown: 60 seconds
[PokéHunter] Base shiny odds: 1 in 8192
```

Season and event details depend on the configured schedules and the
instant when the action runs.

The check does not modify Twitch rewards, enforce cooldowns, perform
encounters, award catches, or apply event selectors to a Pokémon pool.

## Database storage and migrations

Storage code is in `streamerbot/storage/` and is compiled into
`PokeHunter.Storage.dll`.

The current database schema version is `3`.

| Migration                        | Purpose                                              |
| -------------------------------- | ---------------------------------------------------- |
| `001_CreateMigrationHistory.sql` | Creates migration history.                           |
| `002_CreateTrainers.sql`         | Creates trainer storage.                             |
| `003_CreateSeasons.sql`          | Creates seasons and their unlocked-generation table. |

The SQL files are embedded resources in the storage library.

A fresh database receives migrations 1 through 3. Existing supported
databases receive only their missing migrations. A valid version-3
database requires no migration.

Previously applied migration records retain their original timestamps.

Do not edit an already-applied migration to introduce a schema change.
Add a new numbered migration and update the initializer and checks.

Current storage functionality includes:

- Database initialization and migration history.
- Rejection of unsupported newer databases and unrelated databases.
- Trainer profile creation, retrieval, and name updates.
- Season tables and the `StoredSeason` model.

Season registration and retrieval through a repository are not
implemented yet.

See [Database Design](DATABASE.md) for schema details and implementation
limits.

## Manual database checks in Streamer.bot

The current database action sources are:

| Source                                             | Suggested action name                        |
| -------------------------------------------------- | -------------------------------------------- |
| `streamerbot/tests/DatabaseInitializationCheck.cs` | `PokéHunter — Database Initialization Check` |
| `streamerbot/tests/DatabaseRejectionCheck.cs`      | `PokéHunter — Database Rejection Check`      |
| `streamerbot/tests/TrainerStorageCheck.cs`         | `PokéHunter — Trainer Storage Check`         |

Create a separate action and Execute C# Code sub-action for each file.

### Setup

1. Build the Streamer.bot action project.
2. Close Streamer.bot completely.
3. Install the updated `PokeHunter.Storage.dll`.
4. Reopen Streamer.bot.
5. Paste each complete action source into its corresponding sub-action.
6. Set `ProjectFolder` where the source requires it.
7. Add the assembly references below.
8. Save and compile each action before running it.

Required assembly references:

- `PokeHunter.Storage.dll` from Streamer.bot's `dlls` directory.
- `System.Data.SQLite.dll` from Streamer.bot's `dlls` directory.
- The .NET Framework `System.Data.dll`.

On the verified Windows x64 setup, the framework assembly is:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Data.dll
```

The managed SQLite reference alone is insufficient. Follow
[SQLite Setup](SQLITE_SETUP.md) for the native SQLite dependency.

Building the project does not run these manual checks.

### Database initialization check

The initialization check creates or upgrades:

```text
runtime/database-initialization-test.db
```

This is a dedicated test database inside the repository.

The check verifies that:

- Existing migration records are preserved.
- Initialization reaches schema version `3`.
- Repeated initialization preserves all three migration records.
- Trainer, season, and season-generation tables have the expected columns.
- A separate fresh database reaches version `3` successfully.

When upgrading an existing version-2 test database, the previous
migration count is `2`. Later runs against the upgraded database
report `3`.

A successful run ends with:

```text
[PokéHunter] All database initialization checks passed.
```

The persistent test database is retained between runs.
The separate fresh-database fixture is temporary and cleanup is attempted.

Once the persistent database has been upgraded, another run checks
repeated initialization rather than repeating the earlier upgrade.

Column checks do not verify every constraint or future repository rule.

### Database rejection check

The rejection check creates temporary database fixtures.

It verifies that:

- A database with a version higher than
  `DatabaseInitializer.CurrentSchemaVersion` is rejected.
- An unrelated database is rejected.
- The database file bytes remain unchanged after each rejected attempt.

A successful run ends with:

```text
[PokéHunter] All database rejection checks passed.
```

Recompile this action after changing `CurrentSchemaVersion`.
Public constants can be copied into calling code during compilation,
so replacing the storage DLL alone may leave an old constant value
in a previously compiled action.

### Trainer storage check

The trainer check creates a temporary database containing synthetic
trainer profiles.

It verifies that:

- An unknown trainer returns `null`.
- A trainer can be created and read through another repository instance.
- Saving an unchanged profile preserves its timestamps.
- Renaming updates the profile while preserving identity and creation time.
- Different trainers remain separate.
- Repeated saves do not create duplicate trainer rows.
- A blank login name is rejected without changing the saved profile.

A successful run ends with:

```text
[PokéHunter] All trainer storage checks passed.
```

The check does not contact Twitch or require a channel-point redemption.
It uses its own temporary database and attempts to remove it afterward.

### Earlier SQLite persistence check

`streamerbot/tests/SQLitePersistenceCheck.cs` remains a basic SQLite
connection and persistence check.

It uses:

```text
runtime/sqlite-persistence-test.db
```

Its saved run count should increase across executions.

This check complements the migration and repository checks.
It does not replace them.

## Applying later changes

### Configuration-library code

For changes to loaders, validators, selectors, or time-zone support:

1. Build the Streamer.bot action project.
2. Build and run the standalone configuration checks.
3. Close Streamer.bot.
4. Replace the installed `PokeHunter.Core.dll`.
5. Update `TimeZoneConverter.dll` if its dependency version changed.
6. Reopen Streamer.bot.
7. Save and compile affected actions, then run the manual configuration check.

Rebuilding alone does not replace an installed DLL.

### Storage-library code or migration SQL

For changes under `streamerbot/storage/`:

1. Build the Streamer.bot action project.
2. Close Streamer.bot.
3. Replace the installed `PokeHunter.Storage.dll`.
4. Reopen Streamer.bot.
5. Update pasted test source if it changed.
6. Save and compile affected actions.
7. Run the database checks relevant to the change.

For a schema change, also update migration expectations and
[Database Design](DATABASE.md).

Preserve existing regression checks when extending the test actions.

### Action source

For changes to a manual action:

1. Save the source file.
2. Build the action project.
3. Copy the updated source into its Execute C# Code sub-action.
4. Save and compile.
5. Run the action.

Update installed libraries as well if the action depends on changed
library code.

### Standalone check source

For changes inside `tests/configuration/`:

1. Save the changed files.
2. Build the standalone check project.
3. Run the executable only if the build succeeds.

Changes limited to standalone checks do not require updating a pasted
Streamer.bot action or deploying a DLL.

### Configuration values

For changes to `game.json`, `seasons.json`, or `events.json`:

1. Save the JSON file.
2. Run the standalone configuration checks.
3. Rerun the manual configuration action.

The manual action reads all three configuration files on every execution.

Changing JSON values alone does not require rebuilding a DLL.
Rebuild the checks if their source or the configuration library changed.

Configuration refresh behaviour for live gameplay will be documented
when implemented.

## Troubleshooting

### dotnet is unavailable or no SDK is listed

Install the .NET SDK for Windows x64, restart VS Code, then run:

```powershell
dotnet --list-sdks
```

### Streamer.bot references cannot be found

Check `streamerbot/StreamerBot.local.props`.

Its path must identify the installation containing the expected
Streamer.bot DLLs.

### SQLite references or native dependencies cannot be found

Follow [SQLite Setup](SQLITE_SETUP.md).

The projects expect `System.Data.SQLite.dll` inside Streamer.bot's
`dlls` directory. The native SQLite dependency must also be installed
in the documented location.

### System.Data or DbConnection cannot be found

Add the .NET Framework `System.Data.dll` reference to the affected
Execute C# Code sub-action.

A reference in the `.csproj` does not configure the action editor.

Errors about `DbConnectionStringBuilder`, `DbConnection`, `DbCommand`,
or SQLite types not implementing `IDisposable` can result from this
missing reference.

### PokeHunter classes cannot be found inside Streamer.bot

Open the affected Execute C# Code sub-action's References tab.

Confirm that the correct library is referenced:

- `PokeHunter.Core.dll` for configuration classes.
- `PokeHunter.Storage.dll` for storage classes.

Confirm that the installed DLL came from the latest successful build,
then save and compile again.

A `using` statement imports a namespace. It does not add an assembly
reference.

### SeasonSelector cannot be found during a local build

Confirm that this file exists and is saved:

```text
streamerbot/configuration/SeasonSelector.cs
```

It must declare the public `SeasonSelector` class in the
`PokeHunter.Configuration` namespace.

The calling file should import that namespace:

```csharp
using PokeHunter.Configuration;
```

Also confirm that its project references `PokeHunter.Core`.

### A generated DLL is missing from the installation

Build output is created inside the repository. It is not automatically
copied into Streamer.bot.

Use the installation steps in this guide after a successful build.
Close Streamer.bot before replacing installed DLLs.

### A DLL cannot be replaced because it is in use

Exit Streamer.bot completely, including its system-tray instance.

Check for another running instance before retrying the copy.
Using `-Force` does not release a process's file lock.

### Windows hides file extensions

In File Explorer, enable:

**View → Show → File name extensions**

This helps distinguish `.cs`, `.csproj`, `.sql`, and `.dll` files.

### Configuration files cannot be found

For the manual action, check `ProjectFolder`.

It must point to the repository root containing:

```text
config/game.json
config/seasons.json
config/events.json
```

It should not point to the Streamer.bot installation or the
repository's `streamerbot` subdirectory.

For standalone checks, run the documented command from the repository
root or provide absolute configuration paths.

### Configuration checks display the usage message

Supply all three paths, in order:

```powershell
& ".\tests\configuration\bin\Debug\net481\ConfigurationChecks.exe" ".\config\game.json" ".\config\seasons.json" ".\config\events.json"
```

Commands containing only one or two configuration paths are outdated.

### PASS messages appear after a failed build

An older executable may remain from a previous successful build.

Running it does not verify the latest source changes.
Use the guarded build-and-run command in this guide.

### A season or event timestamp is rejected

Include the date, time with seconds, and an explicit offset or `Z`:

```text
2027-01-01T00:00:00+01:00
```

Check that the date is valid and that the ending instant follows
the starting instant.

### Seasons overlap or have a gap

Each season after the first must start at exactly the preceding
season's exclusive ending instant.

An earlier start creates an overlap. A later start creates a gap.
Both are rejected.

Different offsets can describe the same instant. Compare complete
timestamps rather than only their displayed clock times.

### No season is active

Check whether:

- The current instant precedes the first configured season.
- The current instant is at or after the final configured end.
- The computer's clock is correct.
- The action reads the intended configuration file.

Add the next season before the configured schedule ends.
The selector does not reuse expired seasons or invent future ones.

### No event is active

An inactive event period is allowed.

Check the event's `enabled` setting and its start and exclusive end
if an event was expected to be active.

### Shared-library changes do not appear in Streamer.bot

Confirm that you:

1. Built successfully.
2. Closed Streamer.bot.
3. Replaced the correct installed DLL.
4. Reopened Streamer.bot.
5. Saved and compiled affected actions.

If action source changed, replace the pasted source as well.

### An action reports an empty InlineCode ID

Open the affected Execute C# Code sub-action and save and compile it.

Resolve any compilation errors before running the action again.

### CPH is not recognized in VS Code

Check that the editor class inherits:

```csharp
Streamer.bot.Plugin.Interface.CPHInlineBase
```

The inheritance should be inside the `EXTERNAL_EDITOR` branch.

If the command-line build succeeds but VS Code still shows stale errors,
run **Developer: Reload Window** from the Command Palette.
