# Catch Overlay

The catch overlay displays a viewer's profile image and name, a catch
announcement, and the Pokémon sprite.

Streamer.bot sends notifications to a local Node server. OBS receives
them through a browser source and displays each notification in order.

## Current status

The initial Streamer.bot-to-OBS test has been verified.

The current endpoint accepts test notifications for KapteinOle only.
It is not yet connected to the production catching engine.

Overlay tests are recorded in `runtime/overlay-tests.jsonl`.
They do not create trainer records, grant Pokémon, change shiny bonuses,
or write Seasonal or National Dex ownership.

Queue, shiny, muted-audio, and portrait behaviour should be checked using
the manual checklist below.

## Requirements

- Project dependencies installed with `npm ci`.
- Node.js 24 or newer.
- Streamer.bot configured for this project.
- OBS with a Browser Source.
- The generated `data/catalogue/merged-entries.json` file.
- Internet access for Twitch profile lookup and remotely hosted media.

See [Development Guide](DEVELOPMENT.md) for catalogue generation.

## Files

| File                                     | Responsibility                                                |
| ---------------------------------------- | ------------------------------------------------------------- |
| `config/catch-overlay.json`              | Appearance, timing, audio, and queue settings                 |
| `overlay/index.html`                     | Browser-source document                                       |
| `overlay/overlay.css`                    | Layout and presentation                                       |
| `overlay/src/overlay.ts`                 | Notification queue, animation, media, and connection handling |
| `overlay/tsconfig.json`                  | Browser TypeScript configuration                              |
| `scripts/serve-overlay.ts`               | Local file server, notification endpoint, and test logging    |
| `streamerbot/tests/CatchOverlayCheck.cs` | Manual Streamer.bot test action                               |
| `runtime/overlay-tests.jsonl`            | Local test notification records                               |

Compiled browser JavaScript is generated in `overlay/dist/`.
Do not edit or commit that directory.

## Browser and Node code

The project uses separate TypeScript configurations:

- The root configuration checks Node tooling under `scripts/`.
- `overlay/tsconfig.json` checks browser code under `overlay/src/`.

The browser configuration includes the DOM library and sets
`"types": []` to prevent automatic inclusion of Node global types.

ESLint applies shared TypeScript rules to both environments, then assigns
Node or browser globals according to the source location.

## Commands

Run from the repository root:

```powershell
npm run typecheck:overlay
```

Check browser TypeScript without writing compiled files.

```powershell
npm run build:overlay
```

Compile the overlay into `overlay/dist/`.

```powershell
npm run overlay:serve
```

Compile the Node tools and browser overlay, then start the local server.

Keep the server terminal open while using the overlay.
Press Ctrl+C to stop it.

The server listens at:

```text
http://127.0.0.1:8787/
```

It binds to the local computer and serves a fixed list of overlay files.
It does not expose the repository as a general file directory.

## Configure the appearance

Edit `config/catch-overlay.json`.

| Section     | Controls                                                     |
| ----------- | ------------------------------------------------------------ |
| `layout`    | Landscape canvas, notification position, width, and spacing  |
| `text`      | Templates, fonts, sizes, colors, and shadows                 |
| `avatar`    | Profile image visibility, size, and spacing                  |
| `sprite`    | Pokémon image size, pixel rendering, and missing-image label |
| `animation` | Entrance, rise, bobbing, hold, and exit timing               |
| `audio`     | Cry preference, volume, mute setting, and playback limit     |
| `queue`     | Gap between notifications                                    |

`positionX` identifies the horizontal center of the notification.
`positionY` identifies its top edge.

The current landscape configuration is 1980×1080.
Match the Browser Source dimensions to the configured canvas.

For a 1920×1080 canvas, set:

```json
"canvasWidth": 1920,
"positionX": 960
```

Keep the remaining layout properties when editing these values.

Configuration is read when the browser source loads.
Refresh the source after changing configuration.

After changing TypeScript, stop the server, rerun `npm run overlay:serve`,
and refresh the browser source.

## OBS landscape source

Add a Browser Source named:

```text
PokéHunter Catch Overlay
```

Use:

| Setting                                   | Value                            |
| ----------------------------------------- | -------------------------------- |
| URL                                       | `http://127.0.0.1:8787/?debug=1` |
| Width                                     | Match `layout.canvasWidth`       |
| Height                                    | Match `layout.canvasHeight`      |
| Control audio via OBS                     | Enabled                          |
| Shutdown source when not visible          | Disabled                         |
| Refresh browser when scene becomes active | Disabled                         |

The debug label should display `PokéHunter connected`.

Remove `?debug=1` after setup to hide the connection label.

Scale and position the source in OBS as needed.

Keeping the browser loaded preserves its current connection and queue.
The overlay does not currently check whether its OBS source is visible,
so avoid sending notifications while the source is hidden if you expect
viewers to see them.

## Portrait source

For a separate vertical scene, use:

```text
http://127.0.0.1:8787/?layout=portrait&audio=0
```

Set the Browser Source dimensions to 1080×1920.

Portrait mode:

- Uses a 1080×1920 design canvas.
- Centers the notification horizontally.
- Limits its width to leave side margins.
- Caps username and announcement font sizes.
- Retains the configured top position.

The `audio=0` parameter mutes this browser instance to prevent duplicate
cries when both landscape and portrait sources are running.

Ensure the audible source is routed appropriately to the stream outputs.

Each browser instance has its own queue and media-loading timing.
Landscape and portrait animations are not guaranteed to be frame-synchronized.

A responsive overlay does not configure Twitch dual-format broadcasting.
The vertical scene and output must be configured separately.

Preview placement against the mobile interface and chat overlay.

## Streamer.bot test action

Create an action named:

```text
PokéHunter — Catch Overlay Check
```

Add these sub-actions in order:

1. Twitch → User → Get User Info for Target.
2. Core → C# → Execute C# Code.

Set the target login to:

```text
kapteinole
```

The lookup supplies the real Twitch user ID, display name, and profile
image URL. Do not substitute a random viewer or fabricated user ID.

Paste the complete contents of
`streamerbot/tests/CatchOverlayCheck.cs` into Execute C# Code.

Before Execute C# Code, add a Set Argument sub-action named
`pokeHunterProjectRoot`. Set its value to the absolute path of your
local project folder.

Keep personal installation paths out of committed source files.
Before sharing an exported Streamer.bot action, replace its local
path argument with a placeholder.

### Assembly references

The Execute C# Code sub-action needs its normal Streamer.bot references,
plus:

- `Newtonsoft.Json.dll` from the Streamer.bot installation.
- The .NET Framework `System.dll`.

On the verified Windows x64 setup, the framework reference is:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll
```

Add missing references from the C# editor's References tab.

Click Save and Compile before running the action.

A successful command-line C# build does not automatically configure the
references inside Streamer.bot or update its pasted action code.

### Select the test Pokémon

The action defaults to:

```csharp
string entryId = "pikachu:male:normal";
```

For a shiny preview, use:

```csharp
string entryId = "pikachu:male:shiny";
```

The selected entry must exist in `merged-entries.json`.

After editing the source file, update the pasted Streamer.bot code and
compile it again.

Run the whole action so the Twitch lookup executes before the C# code.

## Notification sequence

For each notification:

1. Load the avatar and Pokémon sprite, with a bounded loading wait.
2. Reveal the viewer identity.
3. Reveal the catch message after its configured delay.
4. Raise the sprite after its configured delay.
5. Attempt to play one cry when the sprite begins appearing.
6. Bob the sprite after the rise completes.
7. Hold the notification, then fade it out.
8. Wait the configured queue gap before the next notification.

Message and sprite delays are measured from the notification's start.

The hold begins after the entrance animations have completed.

## Missing media

An unavailable avatar is hidden.

An unavailable Pokémon sprite displays `sprite.fallbackLabel`.

The preferred cry reference is used when present. The fallback reference
is used when the preferred reference is null.

A playback failure does not stop the notification.
The current implementation does not retry a failed cry using the fallback.

A URL being present does not prove that its remote media loads.

## Test records and delivery limits

Accepted test notifications are appended to:

```text
runtime/overlay-tests.jsonl
```

Each line is one JSON object containing the test identity and notification.

This file is local runtime data and must remain excluded from Git.

The test endpoint rejects requests when no overlay client is connected.

An accepted request confirms logging and broadcasting, not that OBS
successfully displayed the notification or played its audio.

Queues are held in browser memory.
Refreshing or closing a browser source clears its pending notifications.

The server does not replay missed notifications after reconnection.
Test logs are not a delivery queue.

These limitations must be considered before connecting production catches.

## Manual verification checklist

Perform these checks before treating an overlay change as ready:

| Check             | Expected result                                             |
| ----------------- | ----------------------------------------------------------- |
| Normal catch      | Correct name, avatar, message, sprite, and animation        |
| Three rapid tests | Notifications appear sequentially                           |
| Shiny catch       | Shiny wording, color, and sprite                            |
| Audio disabled    | Animation completes without a cry                           |
| Portrait source   | Content fits and can be positioned clear of mobile controls |
| Test log          | KapteinOle's real Twitch ID and `isTest: true`              |
| Missing image     | Fallback appears and the queue continues                    |
| Server restart    | Connection recovers; missed events are not replayed         |

## Troubleshooting

### System.Net or InvalidDataException cannot be found

Add the .NET Framework `System.dll` reference inside the Execute C# Code
sub-action, then save and compile.

### Browser type checking reports undici-types errors

Confirm that `overlay/tsconfig.json` includes:

```json
"types": []
```

Keep `"types": ["node"]` in the root TypeScript configuration.

### No overlay is connected

Start `npm run overlay:serve`, open the OBS Browser Source, and use
`?debug=1` to inspect its connection status.

### The test user lookup fails

Run the whole action and confirm Get User Info for Target executes first
with `kapteinole` as the login.

### The cry is played twice

Mute additional browser instances with `audio=0`.
Also check OBS monitoring and desktop-audio capture for duplicated routing.

### Changes do not appear

Recompile changed TypeScript and refresh the OBS Browser Source.

Source-file edits do not automatically update a running page or pasted
Streamer.bot code.
