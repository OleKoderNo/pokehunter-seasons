# C# Development

Build, standalone checks, DLL installation, and pasted Streamer.bot actions are separate
steps. This guide provides the workflow; [Architecture](ARCHITECTURE.md) explains component
boundaries and [Database](DATABASE.md) owns storage behavior.

## Setup

Verified environment: Windows x64, .NET SDK 10.0.401, Streamer.bot 1.0.7, target
.NET Framework 4.8.1, C# 7.3. Installing SDK 10 does not change Streamer.bot's runtime.
Use Microsoft's C# extension for VS Code and follow [SQLite Setup](SQLITE_SETUP.md).

Copy `streamerbot/StreamerBot.local.props.example` to
`streamerbot/StreamerBot.local.props`, then set the folder containing Streamer.bot.exe:

```xml
<Project>
  <PropertyGroup>
    <StreamerBotPath>C:\Tools\Streamer.bot</StreamerBotPath>
  </PropertyGroup>
</Project>
```

Use your actual path without surrounding quotes. The local file is ignored; commit the
example only. Run the following commands from the repository root.

## Projects and build

| Project                                            | Purpose                                                                            |
| -------------------------------------------------- | ---------------------------------------------------------------------------------- |
| `streamerbot/PokeHunter.StreamerBot.csproj`        | Editor/API references and compilation of action source; references Core/Storage    |
| `streamerbot/configuration/PokeHunter.Core.csproj` | Configuration, schedules, catalogue, eligibility, categories, weighted selection   |
| `streamerbot/storage/PokeHunter.Storage.csproj`    | SQLite initialization, embedded migration SQL, repositories                        |
| `tests/configuration/ConfigurationChecks.csproj`   | Standalone configuration checks                                                    |
| `tests/catalogue/CatalogueChecks.csproj`           | Standalone catalogue, lookup, eligibility, category, and weighted-selection checks |

```powershell
dotnet build streamerbot/PokeHunter.StreamerBot.csproj
```

This builds the referenced libraries but does not run checks or install anything.
Core/Storage source is excluded from direct compilation by the action project to avoid
duplicate assemblies. Do not install PokeHunter.StreamerBot.dll; actions use pasted source.
Generated bin/obj directories stay ignored.

## Standalone checks

Build immediately before running. The guards below prevent accidentally executing an
old binary after a failed build.

### Configuration

```powershell
dotnet build tests/configuration/ConfigurationChecks.csproj
if ($LASTEXITCODE -eq 0) {
    & ".\tests\configuration\bin\Debug\net481\ConfigurationChecks.exe" ".\config\game.json" ".\config\seasons.json" ".\config\events.json"
}
```

Three arguments, in order: game, seasons, events. Checks include real-file loading,
invalid milestone size, explicit offsets, season continuity/boundaries, event gaps and
disabled/overlapping schedules, invalid event settings, and time-zone conversions.

### Catalogue and encounter preparation

Generate the merged catalogue first using [Development](DEVELOPMENT.md).

```powershell
dotnet build tests/catalogue/CatalogueChecks.csproj
if ($LASTEXITCODE -eq 0) {
    & ".\tests\catalogue\bin\Debug\net481\win-x64\CatalogueChecks.exe" ".\data\catalogue\merged-entries.json"
}
```

One argument: catalogue path. Checks include real-file loading, rejected schemas/properties,
counterparts, rarity and broken references, duplicate IDs, missing group membership,
nullable media, independent index copies, eligibility, category construction, and
deterministic weighted selection.

Weighted-selection checks cover interval boundaries, proportional weights, single-item
pools, invalid inputs, and input preservation. They do not test a random-number generator
or execute a complete encounter.

Eligibility/category checks use fixed test schedules, not every event in config/events.json.

Both programs return 0 on success and 1 on failure. Invalid-input fixtures use temporary
copies; original JSON and player databases are not modified. Cleanup is attempted and
failures are reported. Standalone projects reference the actual Core library and copy
Newtonsoft.Json beside their executables (`Private=true`). No Streamer.bot restart or
DLL deployment is needed just to run these checks.

### Preserve regression coverage

Check classes are explicitly called, not automatically discovered. Call a new group's
Run method from the appropriate runner. Existing checks must remain active when refactored.
Use fresh fixtures and change only the behavior under test. Expected failures must match
the intended error, not accept any exception. Print PASS only after assertions succeed.

Configuration helpers include CheckAssert, TestJsonFiles, and EventTestFixtures.
Catalogue Program calls CatalogueIndexChecks, EligibilityChecks, and
WeightedSelectionChecks; EligibilityChecks calls EncounterCategoryChecks. These checks cover selected behaviors, not every validation branch.

## Install shared libraries

After a successful build, fully exit Streamer.bot (including its tray instance), then copy:

| Repository build output                                            | Streamer.bot destination      |
| ------------------------------------------------------------------ | ----------------------------- |
| `streamerbot/configuration/bin/Debug/net481/PokeHunter.Core.dll`   | `dlls/PokeHunter.Core.dll`    |
| `streamerbot/configuration/bin/Debug/net481/TimeZoneConverter.dll` | `dlls/TimeZoneConverter.dll`  |
| `streamerbot/storage/bin/Debug/net481/PokeHunter.Storage.dll`      | `dlls/PokeHunter.Storage.dll` |

Reopen Streamer.bot after copying. TimeZoneConverter comes from NuGet restore.
SQLite has separate managed/native dependencies; use its setup guide. Never copy csproj
files as libraries. `-Force` does not bypass a DLL lock held by a running process.

## Run manual Streamer.bot checks

For each action: edit/save source, build, install changed libraries, paste the complete
source into Execute C# Code, add that sub-action's references, save/compile, and run.
Local csproj references do not configure the action editor. Updating source on disk does
not update pasted code.

Where an older action uses ProjectFolder, set the repository path in the pasted local
copy. Keep personal paths out of commits and shared exports. CatchOverlayCheck instead
uses the `pokeHunterProjectRoot` argument described in its dedicated guide.

| Source in `streamerbot/tests/`   | Required additional references           | Expected coverage                                                                    |
| -------------------------------- | ---------------------------------------- | ------------------------------------------------------------------------------------ |
| `GameConfigurationCheck.cs`      | Core, TimeZoneConverter, Newtonsoft.Json | Game/seasons/events load; one timestamp selects/logs season/event                    |
| `DatabaseInitializationCheck.cs` | Storage, System.Data.SQLite, System.Data | Existing history preserved; fresh/repeated initialization reaches schema 3           |
| `DatabaseRejectionCheck.cs`      | Storage, System.Data.SQLite, System.Data | Newer/unrelated database rejected without byte changes                               |
| `TrainerStorageCheck.cs`         | Storage, System.Data.SQLite, System.Data | Create/read/rename, timestamps, no duplicate trainers, invalid name rejection        |
| `SeasonStorageCheck.cs`          | Storage, System.Data.SQLite, System.Data | Equivalent registration, conflicts, invalid generations, international IDs, rollback |
| `SQLitePersistenceCheck.cs`      | System.Data.SQLite, System.Data          | Persistent run counter; see SQLite Setup                                             |
| `CatchOverlayCheck.cs`           | Newtonsoft.Json, System                  | Test notification; see Catch Overlay                                                 |

Core, Storage, TimeZoneConverter, and managed SQLite are under Streamer.bot's dlls folder;
Newtonsoft.Json is in its root. Framework references on the verified setup are:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Data.dll
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll
```

Keep these checks manual, not attached to live rewards. Suggested action names are
`PokéHunter — Database Initialization Check`, etc., matching their source purpose.

The configuration action returns false without an active season; no active event is
normal. Selector counts in its log are not Pokémon counts. It does not perform encounters.

Initialization retains `runtime/database-initialization-test.db`; the basic persistence
action retains `runtime/sqlite-persistence-test.db`. Other storage fixtures are temporary
and synthetic, not viewer data. SeasonStorageCheck needs no ProjectFolder. A previously
upgraded database tests idempotence on later runs, not the original upgrade again.
Recompile calling actions when public constants such as CurrentSchemaVersion change.

## Source map and boundaries

| Area                        | Responsibilities and limits                                                                                                  |
| --------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Configuration root          | Models/loaders/validators, RequiredConfigContractResolver, ExplicitOffsetDateTimeConverter, ConfigurationTimeZone, selectors |
| `configuration/catalogue/`  | Document models, loader, validator, index                                                                                    |
| `configuration/encounters/` | General/Event eligibility IDs, category/group candidates, and weighted selection                                             |
| `storage/`                  | Initialization, migration registry, trainer/season models and repositories                                                   |

RequiredConfigContractResolver requires configuration properties and rejects nulls.
Catalogue models instead use explicit JsonProperty attributes because some media and form
fields intentionally permit null. The catalogue loader reads the file on each call and
currently accepts `sampleOnly: true`. Index lookups do not read files; returned records
are deep copies. The runtime still needs to retain the index and manage refreshes.

Eligibility evaluates a supplied instant and resolves active-event references. Categories
use one requested shiny state, preserve category-specific variants, and omit empty pools.
The weighted selector chooses an item using a supplied number in [0, 1); it does not
generate randomness. Ownership filtering, a complete encounter roll, and catch saving
remain separate work.
See [Data Model](DATA_MODEL.md) for contracts and [Configuration](CONFIGURATION.md) for schedules.

## Editor declarations and formatting

Action files use a unique editor class name under EXTERNAL_EDITOR, while the pasted
Streamer.bot source uses CPHInline:

```csharp
#if EXTERNAL_EDITOR
public class ExampleCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
```

The Execute declaration is likewise conditional:

```csharp
#if EXTERNAL_EDITOR
public new bool Execute()
#else
public bool Execute()
#endif
```

`new` intentionally hides the inherited editor method and avoids CS0114. Copy the
conditional declarations into the action; do not define EXTERNAL_EDITOR in Streamer.bot.
Reusable library classes do not need this pattern.

In VS Code, open a .cs file → Format Document With… → Configure Default Formatter → C#.
Merge this property into your existing settings object, preserving other settings:

```json
"[csharp]": {
  "editor.defaultFormatter": "ms-dotnettools.csharp",
  "editor.formatOnSave": true
}
```

Shift+Alt+F formats manually. Prettier checks do not format or compile C#.

## After changing code

| Change                   | Required follow-up                                                                                        |
| ------------------------ | --------------------------------------------------------------------------------------------------------- |
| Core library             | Build; run affected standalone suites; deploy DLL/recompile actions when testing in Streamer.bot          |
| Storage/migration SQL    | Build; deploy Storage; recompile affected actions; run relevant manual checks                             |
| Action source            | Build; repaste; compile and run the action                                                                |
| Standalone checks only   | Build/run their executable; no deployment                                                                 |
| Game/seasons/events JSON | Run configuration checks; manual configuration action reads files again; no DLL rebuild solely for values |
| Rarity JSON              | Regenerate merged catalogue; run catalogue checks                                                         |

Automatic live reload is not implemented. Source changes and installed copies can differ;
always test the version you intend to use.

## Troubleshooting

| Symptom                                                   | Check                                                                                        |
| --------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| No dotnet SDK                                             | Install Windows x64 SDK, restart VS Code, run `dotnet --list-sdks`                           |
| Missing Streamer.bot references                           | Local props path must identify the installation containing DLLs                              |
| Missing PokeHunter namespace                              | Install and reference the correct Core/Storage DLL; a using statement is not a reference     |
| Missing SeasonSelector locally                            | Save its source under configuration, use PokeHunter.Configuration, reference Core            |
| DbConnection/SQLite IDisposable errors                    | Add System.Data.dll in the action editor; managed SQLite alone is insufficient               |
| Missing System.Net/InvalidDataException in overlay action | Add framework System.dll for System.Net; include `using System.IO;` for InvalidDataException |
| Native SQLite load failure                                | Check x64 e_sqlite3.dll location in SQLite Setup                                             |
| Locked DLL                                                | Fully exit Streamer.bot and other instances before replacing                                 |
| PASS after failed build                                   | Old executable ran; use guarded commands above                                               |
| Configuration usage message                               | Supply all three paths in the documented order                                               |
| Missing files                                             | ProjectFolder/argument must identify repository root; verify full file extensions            |
| Invalid timestamp                                         | Include seconds and offset/Z; ensure end follows start                                       |
| Season gap/overlap                                        | Next start must equal previous end as an instant                                             |
| No active season/event                                    | Check dates, enabled state, clock, and actual config path; event gaps are valid              |
| Changes absent in Streamer.bot                            | Build, close, replace DLLs, reopen, update pasted source, compile                            |
| Empty InlineCode ID                                       | Save/compile the sub-action and resolve compiler errors                                      |
| CPH editor error                                          | Check EXTERNAL_EDITOR inheritance; reload VS Code if build passes but diagnostics are stale  |
