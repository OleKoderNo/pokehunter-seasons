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

This project compiles the reusable game and season configuration
classes into:

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

This project builds a small executable that tests game and season
configuration loading outside Streamer.bot.

It references the shared-library project, so the checks exercise the
shared implementation.

The existing game configuration checks remain active alongside the
season checks.

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

1. Close Streamer.bot.
2. Open `streamerbot/configuration/bin/Debug/net481/` in your repository.
3. Copy `PokeHunter.Core.dll`.
4. Paste it into the `dlls` folder inside your Streamer.bot installation.
5. Reopen Streamer.bot.

For example, if Streamer.bot is installed at `C:\Tools\Streamer.bot`,
the installed library should be:

```text
C:\Tools\Streamer.bot\dlls\PokeHunter.Core.dll
```

Building the project does not automatically install or update this copy.

Do not copy `PokeHunter.Core.csproj` into Streamer.bot. That file contains
build instructions, not the compiled library.

Use Streamer.bot's existing `Newtonsoft.Json.dll`. There is no need to
download a separate JSON library for this setup.

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
  generation lists, and season boundaries.

Season configuration loading has passed initial standalone checks.

The validator requires:

- Schema version `1`.
- A nonempty time-zone name.
- At least one season.
- Nonempty IDs and display names without surrounding whitespace.
- Unique season IDs, compared without regard to capitalization.
- An ending instant later than the starting instant.
- Nonempty generation lists containing distinct positive integers.
- Season schedules that do not overlap.

Gaps between seasons are allowed. Annual durations and cumulative
generation unlocks are not hardcoded requirements.

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

Season starts are inclusive and season ends are exclusive. One season
may end at exactly the instant another begins.

The current validator checks that the named time zone is supplied,
but does not yet resolve it or verify that timestamp offsets match
that zone's rules.

### Current implementation limits

Active-season selection and named time-zone resolution are not yet
implemented.

Season loading has not yet been added to a manual Streamer.bot action.
The existing manual configuration check loads only `config/game.json`.

Configuration loading is not connected to a live catching action.

## Run the standalone configuration checks

From the repository root, build the standalone test program:

```powershell
dotnet build tests/configuration/ConfigurationChecks.csproj
```

This also builds its referenced shared-library project.

After a successful build, run:

```powershell
& ".\tests\configuration\bin\Debug\net481\ConfigurationChecks.exe" ".\config\game.json" ".\config\seasons.json"
```

The executable requires two arguments, in this order:

1. The path to `game.json`.
2. The path to `seasons.json`.

### What the checks verify

1. The supplied game configuration loads successfully.
2. A zero unique-entry milestone size is rejected for the expected reason.
3. The supplied season configuration loads successfully.
4. Adjoining seasons with equivalent timestamps and different offsets
   are accepted, and the supplied offset is preserved.
5. Overlapping seasons are rejected.
6. A timestamp without an explicit UTC offset is rejected.

The original configuration files are not modified.

The invalid game example uses a temporary copy of the supplied game
configuration.

Season boundary tests use a fixed example schedule. This keeps the tests
independent of changes to the creator's real season schedule.

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

With the repository's default game settings, the log should include:

```text
[PokéHunter] Game configuration check passed.
[PokéHunter] Reward cost: 500 | Cooldown: 60 seconds
[PokéHunter] Base shiny odds: 1 in 8192
```

The action also logs the configuration file path.

This confirms that Streamer.bot can load the shared library and use it
to read and validate `config/game.json`.

The action does not load seasons, change Twitch rewards, enforce
cooldowns, or perform encounters.

## Test locations

- `streamerbot/tests/` contains manual test actions executed inside
  Streamer.bot.
- `tests/configuration/` contains the standalone configuration checks.

The manual game configuration check and the standalone checks use
the shared configuration implementation.

### Standalone check structure

The standalone checks are divided by responsibility:

| File                           | Responsibility                                                                      |
| ------------------------------ | ----------------------------------------------------------------------------------- |
| `Program.cs`                   | Reads arguments, runs check groups, reports failures, and cleans up temporary files |
| `GameConfigurationChecks.cs`   | Loads game configuration and checks invalid game settings                           |
| `SeasonConfigurationChecks.cs` | Loads season configuration and checks scheduling and timestamp examples             |
| `CheckAssert.cs`               | Verifies that invalid input is rejected for the expected reason                     |
| `TestJsonFiles.cs`             | Writes temporary JSON examples for the loaders                                      |

These files belong to the standalone executable. They are not compiled
into `PokeHunter.Core.dll` or pasted into Streamer.bot.

The project automatically includes C# files inside its directory.

### How a check run works

1. `Program` receives the game and season configuration paths.
2. It creates a uniquely named temporary directory.
3. It runs the game checks followed by the season checks.
4. Each group loads the real configuration and runs its controlled cases.
5. A failure throws an exception and stops the remaining checks.
6. `Program` reports success with exit code `0`, or failure with exit code `1`.
7. Cleanup is attempted whether the checks pass or fail.

A cleanup failure produces a warning without replacing the test result.

### Temporary examples and fixtures

The game rejection check modifies an in-memory copy of the supplied
game configuration and writes that copy into the temporary directory.

Season checks use a fixture: a small, fixed example schedule created
specifically for testing. Each scenario receives a fresh fixture.

This keeps season boundary checks independent of the creator's actual
schedule and prevents one scenario from changing another's input.

The original configuration files are never overwritten by these checks.

### Adding a check

1. Choose the appropriate configuration-check class.
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

For changes to the configuration classes:

1. Rebuild the project.
2. Build and run the standalone configuration checks.
3. Close Streamer.bot when ready to install the update.
4. Replace the installed `PokeHunter.Core.dll` with the newly built copy.
5. Reopen Streamer.bot.
6. Run the manual game configuration check.

Rebuilding alone does not replace the installed DLL.

The manual action currently checks game configuration only. Use the
standalone checks to verify season configuration loading.

### Action C# code

For changes to a file such as `GameConfigurationCheck.cs`:

1. Save the source file.
2. Build the Streamer.bot action project.
3. Copy the updated source into its Execute C# Code sub-action.
4. Save and compile.
5. Run the action.

### Game configuration values

For changes to `config/game.json`:

1. Save the JSON file.
2. Rerun the manual game configuration check.

This check reads the file on every execution. Changing JSON values does
not require rebuilding the DLL.

### Season configuration values

For changes to `config/seasons.json`:

1. Save the JSON file.
2. Run the standalone configuration checks with both configuration paths.

Changing the JSON values does not require rebuilding the DLL.
Rebuild the checks if their code or the shared-library code changed.

Configuration refresh behaviour for live gameplay will be documented
when implemented.

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

### PokeHunter, GameConfig, or GameConfigLoader cannot be found

In the Streamer.bot action's Execute C# Code editor:

1. Open the References tab.
2. Confirm that `PokeHunter.Core.dll` is listed.
3. If it is missing, add it from the installation's `dlls` folder.
4. Save and compile again.

Adding `using PokeHunter.Configuration;` to the code does not add the
DLL reference. It only lets the code use shorter names for classes in
that namespace.

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

Confirm that this file exists beneath it:

```text
config/game.json
```

For standalone checks, run the documented command from the repository
root or supply absolute paths to both configuration files.

### Configuration checks display the usage message

The executable now requires both configuration paths:

```powershell
& ".\tests\configuration\bin\Debug\net481\ConfigurationChecks.exe" ".\config\game.json" ".\config\seasons.json"
```

The earlier command with only `game.json` is no longer sufficient.

### Season timestamp is rejected

Include the date, time with seconds, and an explicit offset or `Z`.

For example:

```text
2027-01-01T00:00:00+01:00
```

Do not omit the offset. Check that the date itself is valid.

### Seasons overlap

Check that each season starts at or after the preceding season's
exclusive ending instant.

Different offsets can describe the same instant, so compare the complete
timestamps rather than just their displayed clock times.

### Shared-library changes do not appear in Streamer.bot

Confirm that you rebuilt the library and replaced the installed copy.

Close Streamer.bot before replacing the DLL, then reopen it and rerun
the check.

### CPH is not recognized

Check that the editor class inherits
`Streamer.bot.Plugin.Interface.CPHInlineBase` under `EXTERNAL_EDITOR`.

If the command-line build succeeds but VS Code still shows stale errors,
run **Developer: Reload Window** from the Command Palette.
