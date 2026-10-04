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

This project currently compiles the reusable configuration classes into:

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

This project builds a small executable that tests configuration loading
outside Streamer.bot.

It references the shared-library project, so the checks use the same
implementation as the Streamer.bot configuration-check action.

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
2. Open this folder inside your repository:

   ```text
   streamerbot/configuration/bin/Debug/net481/
   ```

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

## Configuration loading and checks

The game configuration code is in `streamerbot/configuration/`:

- `GameConfig.cs` describes the settings structure.
- `RequiredConfigContractResolver.cs` maps property names to camelCase
  and requires configuration properties to be present and non-null.
- `GameConfigLoader.cs` reads the JSON file, deserializes it, and calls
  the validator.
- `GameConfigValidator.cs` checks the supported schema version and
  numerical rules.

These classes are compiled into `PokeHunter.Core.dll`.

Unknown JSON properties are rejected to help catch misspelled settings.
The loader reports invalid settings rather than silently substituting
defaults.

Configuration loading has passed initial standalone checks and a manual
check inside Streamer.bot. It is not yet connected to a live catching
action.

### Run the standalone configuration checks

From the repository root, build the standalone test program:

```powershell
dotnet build tests/configuration/ConfigurationChecks.csproj
```

This also builds its referenced shared-library project.

After a successful build, run:

```powershell
& ".\tests\configuration\bin\Debug\net481\ConfigurationChecks.exe" ".\config\game.json"
```

The checks verify that:

1. The supplied game configuration loads successfully.
2. A temporary copy with a zero unique-entry milestone size is rejected
   for the expected reason.

The original configuration file is not modified.

The program returns exit code `0` on success and `1` on failure.
Temporary test files are removed after execution when possible.

The standalone executable needs Newtonsoft.Json beside it, so its
project reference to that dependency uses `Private=true`.

These checks do not yet cover every validation rule.

### Create the manual Streamer.bot configuration check

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

The action does not change Twitch rewards, enforce cooldowns, or perform
encounters.

### Test locations

- `streamerbot/tests/` contains manual test actions executed inside
  Streamer.bot.
- `tests/configuration/` contains the standalone configuration checks.

Both the manual configuration-check action and the standalone checks
use the shared configuration implementation.

## Applying later changes

The update procedure depends on what changed.

### Shared-library C# code

For changes to the configuration classes:

1. Rebuild the project.
2. Run the standalone configuration checks.
3. Close Streamer.bot.
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

### Game configuration values

For changes to `config/game.json`:

1. Save the JSON file.
2. Rerun the manual configuration check.

This check reads the file on every execution. Changing JSON values does
not require rebuilding the DLL.

This describes the manual check's behaviour. Configuration refresh
behaviour for live gameplay will be documented when implemented.

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

Check the `ProjectFolder` value in the pasted action.

It must point to the repository root containing the `config` folder,
not to the Streamer.bot installation or the `streamerbot` source folder.

Confirm that this file exists beneath it:

```text
config/game.json
```

### Shared-library changes do not appear in Streamer.bot

Confirm that you rebuilt the library and replaced the installed copy.

Close Streamer.bot before replacing the DLL, then reopen it and rerun
the check.

### CPH is not recognized

Check that the editor class inherits
`Streamer.bot.Plugin.Interface.CPHInlineBase` under `EXTERNAL_EDITOR`.

If the command-line build succeeds but VS Code still shows stale errors,
run **Developer: Reload Window** from the Command Palette.
