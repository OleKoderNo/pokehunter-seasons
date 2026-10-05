# C# Development Setup

This guide explains how to edit, build, and test PokéHunter's C# code
using VS Code and Streamer.bot.

The project includes reusable code compiled into a shared library and
action source files pasted into Streamer.bot.

## Requirements

- Windows
- VS Code with Microsoft's C# extension
- .NET SDK 10
- Streamer.bot and SQLite libraries configured using
  [SQLite Setup](SQLITE_SETUP.md)

The setup has been verified with SDK 10.0.401 and Streamer.bot 1.0.7.

The C# projects target .NET Framework 4.8.1. Installing the .NET 10 SDK
does not change the runtime used by Streamer.bot.

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

Use your actual installation path. Do not add quotation marks around it.

The local file is ignored by Git. The example file is committed so
other developers can configure their own installation.

Both C# library projects use this local setting to find their required
Streamer.bot libraries.

## 2. Understand the projects

### Streamer.bot action project

```text
streamerbot/PokeHunter.StreamerBot.csproj
```

This project provides API references and compilation checks for the
Streamer.bot action source files.

Its generated `PokeHunter.StreamerBot.dll` is a development build
artifact. Do not install it into Streamer.bot.

Action source files are copied into Execute C# Code sub-actions.

### Shared-library project

```text
streamerbot/configuration/PokeHunter.Core.csproj
```

This project compiles reusable configuration loading, validation, and
season-selection code into:

```text
PokeHunter.Core.dll
```

Install this DLL into Streamer.bot so actions can call those classes.

The shared library uses the Newtonsoft.Json library supplied with your
Streamer.bot installation.

The outer Streamer.bot project references this project rather than
compiling the configuration classes a second time.

### Standalone configuration-check project

```text
tests/configuration/ConfigurationChecks.csproj
```

This project builds a small executable that tests game configuration,
season configuration, and active-season selection outside Streamer.bot.

It references the shared-library project, so the checks exercise the
shared implementation.

Existing game configuration checks remain active alongside the season
configuration and selection checks.

## 3. Build the C# projects

From the repository root, run:

```powershell
dotnet build streamerbot/PokeHunter.StreamerBot.csproj
```

This builds the shared library and the Streamer.bot action project.

The first build restores the .NET Framework reference assemblies from
NuGet.

The build checks C# compilation and references. It does not execute
actions, modify player data, or install anything into Streamer.bot.

The shared library is generated at:

```text
streamerbot/configuration/bin/Debug/net481/PokeHunter.Core.dll
```

The outer project's build output is:

```text
streamerbot/bin/Debug/net481/PokeHunter.StreamerBot.dll
```

A `.csproj` file contains build instructions. A generated `.dll`
contains compiled code.

Generated `bin` and `obj` folders under `streamerbot` and `tests`
should remain ignored by Git. Commit source files and project files,
not generated build output.

The npm checks cover the TypeScript tooling and supported formatting.
Run the C# build separately when changing C# code.

## 4. Understand the editor declarations

The Streamer.bot action project defines `EXTERNAL_EDITOR` during
local compilation.

Our action files use this to select a unique class name and explicitly
inherit Streamer.bot's base class:

```csharp
#if EXTERNAL_EDITOR
public class SQLitePersistenceCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
```

Each action file should use its own unique editor class name.

The Execute method also has separate declarations:

```csharp
#if EXTERNAL_EDITOR
public new bool Execute()
#else
public bool Execute()
#endif
```

The `new` keyword explicitly hides the inherited method in the editor
build. It avoids warning CS0114.

Inside Streamer.bot, `EXTERNAL_EDITOR` is not defined, so the standard
`CPHInline` class and `public bool Execute()` method are used.

These conditional declarations are for action files. The reusable
configuration classes do not need them.

## 5. Format C# files

Prettier handles the project's supported TypeScript, JSON, and Markdown
files. C# formatting is handled by Microsoft's C# extension.

In VS Code:

1. Open a `.cs` file.
2. Select **Format Document With...** from the right-click menu.
3. Select **Configure Default Formatter**.
4. Choose Microsoft's **C#** formatter.

To format C# files automatically when saving, merge this setting into
your VS Code settings:

```json
"[csharp]": {
  "editor.defaultFormatter": "ms-dotnettools.csharp",
  "editor.formatOnSave": true
}
```

Keep your other existing settings.

You can also format the open file manually with **Shift + Alt + F**.

## 6. Install the shared library

After a successful build:

1. Exit Streamer.bot completely, including any system-tray instance.
2. Open `streamerbot/configuration/bin/Debug/net481/` in your repository.
3. Copy both `PokeHunter.Core.dll` and `TimeZoneConverter.dll`.
4. Paste them into the `dlls` folder inside your Streamer.bot installation.
5. Replace the previous copies when updating.
6. Reopen Streamer.bot.

For an installation at `C:\Tools\Streamer.bot`, the files should be:

- `C:\Tools\Streamer.bot\dlls\PokeHunter.Core.dll`
- `C:\Tools\Streamer.bot\dlls\TimeZoneConverter.dll`

`PokeHunter.Core.dll` depends on TimeZoneConverter for configured
time-zone resolution.

TimeZoneConverter is installed through the shared-library project's
NuGet package reference. Building restores the dependency; creators
do not need to download its DLL separately.

Building does not automatically update the copies inside Streamer.bot.

If copying reports that a DLL is being used by another process, confirm
that Streamer.bot has fully exited. PowerShell's `-Force` option cannot
overwrite a DLL held open by another process.

In the manual action's References tab, reference both installed DLLs
alongside Streamer.bot's existing `Newtonsoft.Json.dll`.

Do not install `.csproj` files or `PokeHunter.StreamerBot.dll`.
Commit source files and package references, not generated DLLs.

## 7. Run an action in Streamer.bot

1. Edit and save the action source file in VS Code.
2. Build the C# project.
3. Install or update the shared library if its code changed.
4. Copy the complete action file into its Execute C# Code sub-action.
5. Ensure that the required assembly references are configured there.
6. Compile and save in Streamer.bot.
7. Run the action and inspect its log output.

You can copy the conditional declarations along with the rest of the
file. Do not define `EXTERNAL_EDITOR` inside Streamer.bot.

Editing the source file does not automatically update the pasted action.

Copying a DLL into Streamer.bot's installation folder and adding it as
an action reference are separate steps.

## Configuration loading

The configuration code is in `streamerbot/configuration/` and is
compiled into `PokeHunter.Core.dll`.

### Shared configuration support

- `RequiredConfigContractResolver.cs` maps property names to camelCase
  and requires configuration properties to be present and non-null.
- `ExplicitOffsetDateTimeConverter.cs` reads timestamps that include
  an explicit UTC offset or `Z`.
- `ConfigurationTimeZone.cs` resolves configured time-zone names and
  reports resolution failures as configuration errors.

Unknown JSON properties are rejected to help catch misspelled settings.
Invalid settings are reported rather than silently replaced with defaults.

### Game configuration

- `GameConfig.cs` describes the structure of `config/game.json`.
- `GameConfigLoader.cs` reads the JSON, deserializes it, and calls
  the validator.
- `GameConfigValidator.cs` checks the supported schema version and
  numerical rules.

Game configuration loading has passed initial standalone checks and
a manual check inside Streamer.bot.

### Season configuration

- `SeasonsConfig.cs` describes the structure of `config/seasons.json`
  and its individual season definitions.
- `SeasonsConfigLoader.cs` reads the JSON, applies the timestamp
  converter, and calls the validator.
- `SeasonsConfigValidator.cs` checks required values, unique season IDs,
  generation lists, and schedule continuity.
- `SeasonSelector.cs` selects the season containing a supplied instant.

Season configuration loading and active-season selection have passed
standalone checks and a manual check inside Streamer.bot.

The validator requires:

- Schema version `1`.
- A nonempty time-zone name.
- At least one season.
- Nonempty IDs and display names without surrounding whitespace.
- Unique season IDs, compared without regard to capitalization.
- A supplied starting timestamp that is not the default value.
- An ending instant later than the starting instant.
- Nonempty generation lists containing distinct positive integers.
- A continuous schedule without overlaps or gaps.

Each season after the first must start at exactly the preceding season's
exclusive end.

Annual durations and cumulative generation unlocks are not hardcoded
requirements.

Add the next season before the final configured season ends. The game
does not automatically invent future seasons or their generation unlocks.

Generation availability against the Pokémon catalogue is not checked yet.

### Season timestamps

Season boundaries use `DateTimeOffset`, preserving the timestamp's
explicit UTC offset.

Accepted examples include:

```text
2027-01-01T00:00:00+01:00
2026-12-31T23:00:00Z
```

These examples represent the same instant. `Z` means UTC.

Timestamps must include seconds and an explicit offset or `Z`.
Up to seven fractional-second digits are also supported.

A timestamp without an offset is rejected:

```text
2027-01-01T00:00:00
```

This prevents configuration loading from silently relying on the
computer's local time zone.

Season starts are inclusive and season ends are exclusive. Consecutive
seasons meet at the same instant.

### Active-season selection

Active-season selection is implemented through:

```csharp
SeasonSelector.FindActive(config, instant)
```

It validates the configuration and returns the season containing the
supplied instant.

The selection rule is:

```text
startsAt <= instant < endsAtExclusive
```

At a shared boundary, the previous season ends and the next season
becomes active immediately.

The selector returns `null` before the first configured season and at
or after the final configured end. Invalid schedules throw an exception.

Selection compares instants, accounting for their explicit UTC offsets.
The order of season definitions in the configuration does not determine
which season is selected.

The caller supplies the instant. Tests use fixed timestamps, while the
manual Streamer.bot action captures `DateTimeOffset.UtcNow` once per run.

Selecting a season does not create, modify, or archive collection records.

### Current implementation limits

Game and season configuration loading, active-season selection, and
configured time-zone resolution are connected to the manual Streamer.bot
configuration check.

`ConfigurationTimeZone.cs` resolves supported IANA or Windows time-zone
identifiers through TimeZoneConverter. Unknown or unavailable zones
are rejected during season validation.

The named zone controls local-time display. Explicit timestamp offsets
continue to define season boundaries. The validator does not require a
timestamp's written offset to equal the named zone's local offset.

This allows equivalent UTC timestamps and other explicit-offset
representations to remain valid.

The manual check logs the same instant in UTC and the configured zone.
Time-zone conversion uses the operating system's time-zone rules.

Configuration loading is not connected to a live catching action.
Event and overlay configuration loaders are not implemented yet.

## Run the standalone configuration checks

From the repository root, run:

```powershell
dotnet build tests/configuration/ConfigurationChecks.csproj

if ($LASTEXITCODE -eq 0) {
    & ".\tests\configuration\bin\Debug\net481\ConfigurationChecks.exe" ".\config\game.json" ".\config\seasons.json" ".\config\events.json"
}
```

Building the test project also builds its referenced shared library.

The condition runs the checks only when the build succeeds. This
prevents accidentally running an older executable after a failed build.

The executable requires three arguments, in this order:

1. The path to `game.json`.
2. The path to `seasons.json`.
3. The path to `events.json`.

### How the checks are organized

| File                           | Responsibility                                                                                                     |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------ |
| `Program.cs`                   | Runs the checks, manages temporary files, and reports success or failure.                                          |
| `GameConfigurationChecks.cs`   | Checks the real game configuration and rejection of a zero milestone size.                                         |
| `SeasonConfigurationChecks.cs` | Checks season loading, equivalent adjoining timestamps, overlaps, and explicit offsets.                            |
| `SeasonSelectionChecks.cs`     | Checks season boundaries, offset equivalence, definition order, and gap rejection.                                 |
| `TimeZoneChecks.cs`            | Checks time-zone resolution, winter and summer conversions, and invalid-zone rejection.                            |
| `EventTestFixtures.cs`         | Creates fresh event configurations for independent checks.                                                         |
| `EventConfigurationChecks.cs`  | Checks event loading, selection boundaries, disabled events, gaps, empty schedules, and selected invalid settings. |
| `CheckAssert.cs`               | Checks that invalid configurations fail for the expected reason.                                                   |
| `TestJsonFiles.cs`             | Writes temporary JSON fixtures used by the checks.                                                                 |

The standalone project references `PokeHunter.Core`, so it tests the
shared configuration implementation used by the Streamer.bot actions.

### Event checks

The event checks verify that:

- The supplied event configuration loads.
- Exact starts are included and exact ends are excluded.
- Equivalent timestamps with different offsets select the same event.
- Definition order does not affect selection.
- Disabled events are ignored during selection and overlap checks.
- Gaps and an empty event schedule are accepted.
- Overlapping enabled events are rejected.
- Timestamps without explicit offsets are rejected.
- A zero category weight is rejected.
- Unsupported Pokémon types are rejected.
- A missing Legendary restriction setting is rejected.

Existing game, season, and time-zone checks remain in place.

These checks cover specific behaviours, not every possible invalid
configuration or future encounter rule.

### Results and temporary files

The program returns exit code `0` on success and `1` on failure.

Invalid configuration examples are written to a temporary directory.
The supplied configuration files are not modified. Temporary files
are removed after execution when possible.

Event loading and selection are verified through both the standalone
checks and the manual Streamer.bot configuration action.

### Manual configuration check in Streamer.bot

The action source is `streamerbot/tests/GameConfigurationCheck.cs`.

It loads and validates:

- `config/game.json`
- `config/seasons.json`
- `config/events.json`

It captures the current UTC time once and uses that same instant to
select the active season and event. Local-time logs use the configured
time zone for each schedule.

The action logs the active season's unlocked generations and the
active event's weight, generation restrictions, and inclusion-selector
counts.

Selector counts are not Pokémon counts. An evolution-family selector,
for example, may eventually match several Pokémon in the catalogue.

No active event is a normal result and does not fail the check.
No active season produces a warning and returns false.
Loading or validation errors are logged and return false.

This action verifies configuration integration only. It does not
perform encounters, award catches, or apply event rules to a Pokémon
pool.

After changing shared configuration code, rebuild and replace
`PokeHunter.Core.dll` while Streamer.bot is fully closed.

After changing the action source, copy the updated source into its
Execute C# Code sub-action, compile, and save it.

### What the checks verify

1. The supplied game configuration loads successfully.
2. A zero unique-entry milestone size is rejected for the expected reason.
3. The supplied season configuration loads successfully.
4. Adjoining seasons with equivalent timestamps and different offsets
   are accepted, and the supplied offset is preserved.
5. Overlapping seasons are rejected.
6. A timestamp without an explicit UTC offset is rejected.
7. No season is selected before the first configured start.
8. A season is selected at its exact start.
9. The previous season remains active immediately before a transition.
10. The next season is selected at the exact transition.
11. Expressing the same transition with another offset does not change
    the selected season.
12. The final season remains active immediately before its end.
13. No season is selected at or after the final configured end.
14. Reversing the order of season definitions does not change selection.
15. A gap between configured seasons is rejected.

The original configuration files are not modified.

The invalid game example uses a temporary copy of the supplied game
configuration.

Season scenarios use fixed example schedules. This keeps the checks
independent of changes to the creator's real schedule and today's date.

The program returns exit code `0` on success and `1` on failure.
Temporary test files are removed after execution when possible.

The standalone executable needs Newtonsoft.Json beside it, so its
assembly reference to that dependency uses `Private=true`.

These checks do not yet cover every validation rule.

### Expected output

With the repository's current default settings:

```text
PASS: The real game configuration loaded.
Reward cost: 500 | Cooldown: 60 seconds
PASS: A zero milestone size was rejected.
PASS: The real season configuration loaded.
Configured seasons: 3 | Time zone: Europe/Oslo
PASS: Adjoining seasons with equivalent timestamps were accepted.
PASS: Overlapping seasons were rejected.
PASS: A timestamp without an explicit offset was rejected.
PASS: Season selection respects inclusive starts and exclusive ends.
PASS: Season selection is independent of definition order.
PASS: A gap between seasons was rejected.
All configuration checks passed.
```

Reported settings and season counts will reflect your configuration
if you customize the files.

## Create the manual Streamer.bot configuration check

First, build and install the shared library as described above.

Then:

1. Create an action named `PokéHunter — Game Configuration Check`.
2. Add an Execute C# Code sub-action.
3. Copy the complete contents of
   `streamerbot/tests/GameConfigurationCheck.cs` into the code editor.
4. Set `ProjectFolder` to the absolute path of your project folder.
5. Open the References tab.
6. Right-click the reference list and choose Add reference from file.
7. Select `PokeHunter.Core.dll` from your Streamer.bot `dlls` folder.
8. Ensure `Newtonsoft.Json.dll` from the Streamer.bot installation folder
   is also referenced.
9. Save and compile.
10. Run the action manually and inspect the log.

Despite its original name, this action checks both game and season
configuration.

### What the manual action does

1. Reads and validates `config/game.json`.
2. Reads and validates `config/seasons.json`.
3. Captures the current UTC instant.
4. Selects the season containing that instant.
5. Logs game settings, the configured season count, and both file paths.
6. Logs the active season, unlocked generations, and season boundaries.

If both configurations are valid but no season covers the checked
instant, the action logs a warning and returns `false`.

If loading or validation fails, it logs the exception and returns `false`.

The action does not change Twitch rewards, enforce cooldowns, perform
encounters, or modify collection records.

### Expected manual output

With the default settings, when Season 1 is active, the log includes:

```text
[PokéHunter] Game configuration check passed.
[PokéHunter] Reward cost: 500 | Cooldown: 60 seconds
[PokéHunter] Base shiny odds: 1 in 8192
[PokéHunter] Season configuration check passed. Configured seasons: 3
[PokéHunter] Active season: Season 1 (season-1)
[PokéHunter] Unlocked generations: 1
```

It also logs:

- The checked instant in UTC.
- The game configuration file path.
- The season configuration file path.
- The selected season's start and exclusive end, including their offsets.

The selected season depends on the configured schedule and the time
the action runs.

A UTC timestamp in the message may differ from the local timestamp
shown by Streamer.bot's log. These can represent the same instant.

## Test locations

- `streamerbot/tests/` contains manual test actions executed inside
  Streamer.bot.
- `tests/configuration/` contains the standalone configuration checks.

The manual configuration check and the standalone checks use the shared
configuration implementation.

### Standalone check structure

The standalone checks are divided by responsibility:

| File                           | Responsibility                                                                          |
| ------------------------------ | --------------------------------------------------------------------------------------- |
| `Program.cs`                   | Reads arguments, runs check groups, reports failures, and cleans up temporary files     |
| `GameConfigurationChecks.cs`   | Loads game configuration and checks invalid game settings                               |
| `SeasonConfigurationChecks.cs` | Loads season configuration and checks scheduling and timestamp examples                 |
| `SeasonSelectionChecks.cs`     | Checks exact season boundaries, offset equivalence, definition order, and gap rejection |
| `CheckAssert.cs`               | Verifies that invalid input is rejected for the expected reason                         |
| `TestJsonFiles.cs`             | Writes temporary JSON examples for the loaders                                          |

These files belong to the standalone executable. They are not compiled
into `PokeHunter.Core.dll` or pasted into Streamer.bot.

The project automatically includes C# files inside its directory.

### How a check run works

1. `Program` receives the game and season configuration paths.
2. It creates a uniquely named temporary directory.
3. It runs the game configuration checks.
4. It runs the season configuration checks.
5. It runs the season-selection checks.
6. A failure throws an exception and stops the remaining checks.
7. `Program` reports success with exit code `0`, or failure with exit code `1`.
8. Cleanup is attempted whether the checks pass or fail.

A cleanup failure produces a warning without replacing the test result.

### Temporary examples and fixtures

The game rejection check modifies an in-memory copy of the supplied
game configuration and writes that copy into the temporary directory.

Season configuration checks use a fixture: a small, fixed JSON example
created specifically for testing. Each scenario receives a fresh fixture.

Season-selection checks create fixed configuration objects directly in
C#. JSON parsing is already exercised by the season configuration checks.

This keeps boundary checks independent of the creator's actual schedule
and prevents one scenario from changing another's input.

The original configuration files are never overwritten by these checks.

### Adding a check

1. Choose the appropriate check class.
2. Add a method with a name describing the behaviour being checked.
3. Call that method from the class's `Run` method.
4. Use a fresh valid example and change only what the scenario requires.
5. Use `TestJsonFiles.Write` when the loader needs a temporary file.
6. For expected validation failures, use `CheckAssert.Rejected` with
   message fragments identifying the intended rejection reason.
7. Print the PASS message only after all assertions succeed.
8. Build and run the complete executable.

`CheckAssert.Rejected` requires `InvalidDataException`. An unrelated
exception, an unexpected rejection reason, or invalid input being
accepted causes the check to fail.

For a new check group, add its own class and call its `Run` method from
`Program`. Creating a method or file alone does not execute a check.

### Preserving regression checks

Existing checks remain active when new checks are added. They help
detect regressions: changes that break previously working behaviour.

Moving a check into another file should preserve its inputs, assertions,
and expected results.

Change or remove an existing check only when its expected behaviour has
deliberately changed, and explain that change in the commit.

These are lightweight standalone checks, not an automatically discovered
test suite. New checks must be called explicitly.

## Applying later changes

The update procedure depends on what changed.

### Shared-library C# code

For changes to configuration or season-selection classes:

1. Rebuild the Streamer.bot action project and its referenced library.
2. Build and run the standalone configuration checks.
3. Close Streamer.bot when ready to install the update.
4. Replace the installed `PokeHunter.Core.dll` with the newly built copy.
5. Reopen Streamer.bot.
6. Run the manual configuration check.

Rebuilding alone does not replace the installed DLL.

### Action C# code

For changes to a file such as `GameConfigurationCheck.cs`:

1. Save the source file.
2. Build the Streamer.bot action project.
3. Copy the updated source into its Execute C# Code sub-action.
4. Save and compile.
5. Run the action.

Update the installed shared library as well if the action depends on
new or changed library code.

### Standalone check code

For changes inside `tests/configuration/`:

1. Save the changed files.
2. Build the standalone check project.
3. Run the executable only if the build succeeds.

Changes limited to standalone checks do not require installing a new
DLL or updating a pasted Streamer.bot action.

### Game configuration values

For changes to `config/game.json`:

1. Save the JSON file.
2. Run the standalone configuration checks.
3. Rerun the manual Streamer.bot configuration check.

The manual check reads both configuration files on every execution.
Changing JSON values does not require rebuilding the DLL.

### Season configuration values

For changes to `config/seasons.json`:

1. Save the JSON file.
2. Run the standalone configuration checks.
3. Rerun the manual Streamer.bot configuration check.

The manual check selects the active season again using the current
instant and the newly loaded schedule.

Changing JSON values does not require rebuilding the DLL.
Rebuild the checks if their code or the shared-library code changed.

Configuration refresh behaviour for live gameplay will be documented
when implemented.

## Time-zone verification

`tests/configuration/TimeZoneChecks.cs` runs alongside all existing
configuration and season-selection checks. `Program.cs` explicitly
calls `TimeZoneChecks.Run()`.

The checks verify that:

1. `Europe/Oslo` resolves successfully.
2. A fixed winter instant converts to Oslo with offset `+01:00`.
3. A fixed summer instant converts to Oslo with offset `+02:00`.
4. Both conversions preserve the original instant.
5. Season validation rejects an unknown time-zone identifier.

The additional output appears before the final success message:

- `PASS: Europe/Oslo resolved successfully.`
- `PASS: Oslo winter conversion uses UTC+01:00.`
- `PASS: Oslo summer conversion uses UTC+02:00.`
- `PASS: Season validation rejects an unknown time zone.`

The manual Streamer.bot check also logs the checked instant in UTC and
the configured local zone. Both values describe the same moment.

Changing `timeZone` changes local-time display, not the instants specified
by season boundary timestamps. To move a season boundary, edit its
timestamp explicitly.

## Troubleshooting

### dotnet is unavailable or no SDK is listed

Install the .NET SDK for Windows x64, restart VS Code, then run:

```powershell
dotnet --list-sdks
```

### Streamer.bot references cannot be found

Check `StreamerBot.local.props` and confirm that its path contains the
installed Streamer.bot DLLs.

### SQLite references cannot be found

Follow [SQLite Setup](SQLITE_SETUP.md). The Streamer.bot action project
expects `System.Data.SQLite.dll` inside Streamer.bot's `dlls` folder.

### PokeHunter or its configuration classes cannot be found

In the Streamer.bot action's Execute C# Code editor:

1. Open the References tab.
2. Confirm that `PokeHunter.Core.dll` is listed.
3. If it is missing, add it from the installation's `dlls` folder.
4. Confirm that the installed DLL is from the latest successful build.
5. Save and compile again.

Adding `using PokeHunter.Configuration;` to the code does not add the
DLL reference. It only lets the code use shorter names for classes in
that namespace.

### SeasonSelector cannot be found during a local build

Confirm that this file exists and is saved:

```text
streamerbot/configuration/SeasonSelector.cs
```

It must declare the public `SeasonSelector` class inside the
`PokeHunter.Configuration` namespace.

The calling file should import that namespace:

```csharp
using PokeHunter.Configuration;
```

Rebuild the project after correcting the file or import.

### PokeHunter.Core.dll is missing from the installation folder

The build creates the DLL inside the repository. It does not copy it
into Streamer.bot automatically.

After a successful build, copy it from:

```text
streamerbot/configuration/bin/Debug/net481/PokeHunter.Core.dll
```

Into the `dlls` folder inside your Streamer.bot installation.

Close Streamer.bot before installing or replacing the DLL.

### Windows hides file extensions

In File Explorer, enable:

**View → Show → File name extensions**

This makes it easier to distinguish `.cs`, `.csproj`, and `.dll` files.

### Configuration file cannot be found

For the manual Streamer.bot action, check the `ProjectFolder` value.

It must point to the repository root containing the `config` folder,
not to the Streamer.bot installation or the `streamerbot` source folder.

Confirm that both files exist beneath it:

```text
config/game.json
config/seasons.json
```

For standalone checks, run the documented command from the repository
root or supply absolute paths to both configuration files.

### Configuration checks display the usage message

The executable requires both configuration paths:

```powershell
& ".\tests\configuration\bin\Debug\net481\ConfigurationChecks.exe" ".\config\game.json" ".\config\seasons.json"
```

The earlier command with only `game.json` is no longer sufficient.

### PASS messages appear after a failed build

An older executable may still exist from a previous successful build.
Running it does not verify the latest source changes.

Use the guarded build-and-run command in this guide so the executable
runs only when the build succeeds.

### Season timestamp is rejected

Include the date, time with seconds, and an explicit offset or `Z`.

For example:

```text
2027-01-01T00:00:00+01:00
```

Do not omit the offset. Check that the date itself is valid.

### Seasons overlap or have a gap

Each season after the first must start at exactly the preceding season's
exclusive ending instant.

An earlier start creates an overlap. A later start creates a gap.
Both are rejected.

Different offsets can describe the same instant, so compare the complete
timestamps rather than just their displayed clock times.

### No season is active

A valid schedule can still be outside its active date range.

Check:

- Whether the current instant is before the first configured start.
- Whether the current instant is at or after the final configured end.
- Whether the computer's clock is correct.
- Whether the action is reading the intended configuration file.

The selector does not reuse an expired season or create a future one.
Add the next season definition before the configured schedule ends.

### Shared-library changes do not appear in Streamer.bot

Confirm that you rebuilt the library and replaced the installed copy.

Close Streamer.bot before replacing the DLL, then reopen it and rerun
the check.

If the action source also changed, paste the updated source into its
Execute C# Code sub-action and save and compile again.

### CPH is not recognized

Check that the editor class inherits
`Streamer.bot.Plugin.Interface.CPHInlineBase` under `EXTERNAL_EDITOR`.

If the command-line build succeeds but VS Code still shows stale errors,
run **Developer: Reload Window** from the Command Palette.
