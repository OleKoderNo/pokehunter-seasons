# SQLite Setup and Persistence Test

PokéHunter Seasons will use SQLite to store player progress locally.

The current test verifies that Streamer.bot can create a database, commit
an update, reopen the database, and retain the saved value after restarting.

Player collections and catching logic are not implemented by this test.

## Verified environment

- Streamer.bot 1.0.7, 64-bit
- Windows with .NET Framework 4.8.1
- System.Data.SQLite 2.0.4
- SQLite engine 3.53.4

Other combinations have not yet been verified for this project.

## 1. Download the libraries

Download the packages from:

- https://www.nuget.org/packages/System.Data.SQLite/2.0.4
- https://www.nuget.org/packages/SQLite/3.53.4

Choose **Download package** on each page.

Rename each downloaded file from `.nupkg` to `.zip`, then extract each
archive into its own folder.

If Windows hides file extensions, enable **View → Show → File name
extensions** in File Explorer first.

## 2. Copy the library files

Close Streamer.bot.

The Streamer.bot installation folder is the folder containing
`Streamer.bot.exe`. It is separate from the PokéHunter Seasons project.

Copy these files:

| Source inside extracted package                         | Destination inside Streamer.bot |
| ------------------------------------------------------- | ------------------------------- |
| System.Data.SQLite: `lib/net471/System.Data.SQLite.dll` | `dlls/System.Data.SQLite.dll`   |
| SQLite: `runtimes/win-x64/native/e_sqlite3.dll`         | `e_sqlite3.dll`                 |

Create the `dlls` folder if it does not exist.

The native `e_sqlite3.dll` file belongs directly beside `Streamer.bot.exe`.
Use the `win-x64` version for a 64-bit Streamer.bot process.

Reopen Streamer.bot after copying the files.

## 3. Configure the test

Open `streamerbot/tests/SQLitePersistenceCheck.cs` in your editor.

Change `ProjectFolder` to the absolute path of your own project folder.
This is the folder containing the project's README.

For example:

```csharp
private const string ProjectFolder =
    @"C:\Projects\pokehunter-seasons";
```

The `@` allows Windows backslashes to be written directly in the string.

The test creates:

```text
runtime/sqlite-persistence-test.db
```

This is a disposable test database. It does not contain real player
progress and is separate from the planned production database.

## 4. Create the Streamer.bot action

1. Create an action named **PokéHunter — SQLite Check**.
2. Add **Core → C# → Execute C# Code**.
3. Paste the complete contents of `SQLitePersistenceCheck.cs`.
4. Open the **References** tab.
5. Add a reference to `dlls/System.Data.SQLite.dll` from your Streamer.bot
   installation.
6. If missing, add the framework assembly:

   ```text
   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Data.dll
   ```

7. Compile and save.

Do not add `e_sqlite3.dll` as a compiler reference. It is the native engine
loaded when the code runs.

Keep this test action manual; do not connect it to a channel-point reward.

Changes to the source file in VS Code do not automatically update the
pasted code in Streamer.bot. Copy the updated code into the sub-action
after editing.

## 5. Verify persistence

Run the action once. With a fresh test database, expect:

```text
[PokéHunter] Persistence check passed. Saved run count: 1
```

Close Streamer.bot completely, reopen it, and run the action again.

Expect:

```text
[PokéHunter] Persistence check passed. Saved run count: 2
```

Each successful execution increases the counter by one. Higher numbers
are normal if you have already run the test.

Run the test one execution at a time.

The test also logs the database's absolute path.

## What this verifies

- Streamer.bot can load the SQLite libraries.
- SQLite can create a database in the project's runtime folder.
- A transaction can commit a counter update.
- A fresh connection can read the committed value.
- The saved value survives a normal Streamer.bot restart.

This test does not verify concurrent writes, rollback behaviour, player
collection constraints, or recovery from an interrupted redemption.

## Troubleshooting

### Missing System.Data or SQLiteConnection during compilation

Check the C# sub-action's References tab.

Both `System.Data.SQLite.dll` and `System.Data.dll` must be referenced.
The **Find Refs** button may not discover everything automatically.

### Unable to load e_sqlite3

Check that the Windows x64 `e_sqlite3.dll` is directly beside
`Streamer.bot.exe`, then restart Streamer.bot.

### VS Code reports that no .NET SDKs were found

This is an editor tooling issue. Install the .NET SDK for Windows x64
and restart VS Code.

The SDK alone does not configure Streamer.bot or SQLite references in
the editor. The test is compiled and executed inside Streamer.bot.

### Counter starts at 1 again

Check the database path printed in the log.

Changing `ProjectFolder`, moving or deleting the database, or using a
different project copy can cause the test to create a new database.

## Git and cleanup

Keep `runtime/` in `.gitignore`. Do not commit the generated database.

Commit the test source and this guide.

To reset the test, close Streamer.bot and delete only:

```text
runtime/sqlite-persistence-test.db
```

The next execution starts the counter at 1 again.
