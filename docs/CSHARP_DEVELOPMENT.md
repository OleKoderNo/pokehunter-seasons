# C# Development Setup

This guide explains how to edit and check Streamer.bot action code in
VS Code.

The C# project provides API references and compilation checks. Actions
are still executed inside Streamer.bot.

## Requirements

- Windows
- VS Code with Microsoft's C# extension
- .NET SDK 10
- Streamer.bot and SQLite libraries configured using
  [SQLite Setup](SQLITE_SETUP.md)

The setup has been verified with SDK 10.0.401 and Streamer.bot 1.0.7.

The project targets .NET Framework 4.8.1. Installing the .NET 10 SDK
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

## 2. Build the C# project

From the repository root, run:

```powershell
dotnet build streamerbot/PokeHunter.StreamerBot.csproj
```

The first build restores the .NET Framework reference assemblies from
NuGet.

The build checks C# compilation and references. It does not execute
actions, modify player data, or install anything into Streamer.bot.

Generated files in `streamerbot/bin/` and `streamerbot/obj/` are ignored
by Git.

The npm checks cover the TypeScript tooling and supported formatting.
Run the C# build separately when changing C# code.

## 3. Understand the editor declarations

The project defines `EXTERNAL_EDITOR` during local compilation.

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

## 4. Format C# files

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

## 5. Run an action in Streamer.bot

1. Edit and save the source file in VS Code.
2. Build the C# project.
3. Copy the complete action file into its Execute C# Code sub-action.
4. Ensure that the required assembly references are configured there.
5. Compile and save in Streamer.bot.
6. Run the action and inspect its log output.

You can copy the conditional declarations along with the rest of the
file. Do not define `EXTERNAL_EDITOR` inside Streamer.bot.

Editing the source file does not automatically update the pasted action.

The generated `PokeHunter.StreamerBot.dll` is a compilation artifact.
This workflow uses pasted action source; it does not deploy that DLL.

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

Follow [SQLite Setup](SQLITE_SETUP.md). The C# project expects
`System.Data.SQLite.dll` inside Streamer.bot's `dlls` folder.

### CPH is not recognized

Check that the editor class inherits
`Streamer.bot.Plugin.Interface.CPHInlineBase` under `EXTERNAL_EDITOR`.

If the command-line build succeeds but VS Code still shows stale errors,
run **Developer: Reload Window** from the Command Palette.
