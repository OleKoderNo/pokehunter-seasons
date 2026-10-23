# Catch Overlay

The working preview displays KapteinOle's avatar/name, a catch message, a rising/bobbing
Pokémon sprite, and one cry. Streamer.bot posts to a local Node server; browser sources
receive notifications and display them sequentially.

**Test-only:** the endpoint accepts KapteinOle previews, not production catches. Records
in `runtime/overlay-tests.jsonl` do not grant Pokémon, create trainers, change bonuses,
or update either Dex. The initial Streamer.bot-to-OBS path is verified; use the checklist
below for other behavior rather than assuming every case has been manually verified.

## Start the server

Requirements: Node.js 24+, installed dependencies (`npm ci`), configured Streamer.bot,
OBS Browser Source, and `data/catalogue/merged-entries.json`. Twitch lookup and remote
media require internet. Generate the catalogue using [Development](DEVELOPMENT.md).

```powershell
npm run overlay:serve
```

This builds Node tools/browser code and serves `http://127.0.0.1:8787/` locally.
Keep the terminal open; Ctrl+C stops it. The server serves fixed overlay routes, not a
general repository directory. Separate commands are `npm run typecheck:overlay` and
`npm run build:overlay`.

## Add OBS sources

| Setting                           | Landscape                                | Portrait                                         |
| --------------------------------- | ---------------------------------------- | ------------------------------------------------ |
| URL                               | `http://127.0.0.1:8787/?debug=1`         | `http://127.0.0.1:8787/?layout=portrait&audio=0` |
| Width × height                    | Match layout config; currently 1980×1080 | 1080×1920                                        |
| Control audio via OBS             | Enabled                                  | Enabled; this instance is muted by audio=0       |
| Shutdown when not visible         | Disabled                                 | Disabled                                         |
| Refresh when scene becomes active | Disabled                                 | Disabled                                         |

Suggested name: **PokéHunter Catch Overlay**. The debug label should say
`PokéHunter connected`; remove debug=1 after setup. Scale/position the source in OBS.

Portrait mode centers content, limits width and font sizes, and keeps the configured top
position. It does not configure Twitch dual-format output; configure the vertical scene
and stream output separately, and check placement against mobile controls/chat.

Each browser has an independent queue/loading timeline; sources are not frame-synchronized.
Mute extra instances and route the audible source appropriately to avoid duplicate cries.
A hidden but connected source still consumes notifications.

## Create the Streamer.bot preview action

Create **PokéHunter — Catch Overlay Check** with these sub-actions in order:

1. **Twitch → User → Get User Info for Target**, login `kapteinole`.
2. **Set Argument**, name `pokeHunterProjectRoot`, value your absolute repository folder.
3. **Core → C# → Execute C# Code**, containing the full
   `streamerbot/tests/CatchOverlayCheck.cs` source.

Use KapteinOle's real lookup identity/avatar, not random viewers or fabricated IDs.
Keep the local path in the action argument, not committed source; sanitize shared exports.

Add Newtonsoft.Json.dll from Streamer.bot's root and the framework System.dll reference:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll
```

Save/compile and run the **whole action**, so lookup and argument sub-actions execute first.
A successful local C# build does not configure these references or update pasted code.

The selected entry must exist in the merged catalogue. For previews edit the action's
existing entryId assignment, then repaste and compile:

```csharp
string entryId = "pikachu:male:normal";
```

Use `pikachu:male:shiny` for a shiny preview. Do not add a second declaration.

## Appearance and settings

Edit `config/catch-overlay.json` and refresh the Browser Source. The current preview reads
configuration at page load; source-code changes also require rebuilding/restarting the
server and refreshing the page. See the JSON for exact current values.

Top-level `schemaVersion` is 1. Top-level `enabled: false` disables the preview visuals
and audio; it does not implement any gameplay toggle. Re-enabling does not replay missed tests.

### Layout, text, avatar, and sprite

| Property under its section                                | Effect                                                                     |
| --------------------------------------------------------- | -------------------------------------------------------------------------- |
| `layout.canvasWidth`, `canvasHeight`                      | Landscape design size; match OBS source dimensions                         |
| `layout.positionX`, `positionY`                           | Notification horizontal center and top edge                                |
| `layout.width`                                            | Notification width/text wrapping                                           |
| `layout.textGapPixels`, `spriteGapPixels`                 | Gaps between identity/message and message/sprite                           |
| `text.usernameTemplate`                                   | Viewer text, normally `{displayName}`                                      |
| `text.catchTemplate`, `shinyCatchTemplate`                | Message using `{pokemonName}`; retain explicit shiny wording               |
| `text.fontFamily`                                         | Available CSS font/fallback list; does not download fonts                  |
| `text.usernameFontSizePixels`, `messageFontSizePixels`    | Text sizes; portrait mode caps them                                        |
| `text.usernameColor`, `messageColor`, `shinyMessageColor` | Text colors                                                                |
| `text.shadowColor`, `shadowBlurPixels`                    | Text shadow appearance; zero blur removes blur, not necessarily the shadow |
| `avatar.enabled`, `sizePixels`, `gapPixels`               | Profile-image visibility, size, and spacing                                |
| `sprite.sizePixels`                                       | Aspect-preserving square display box                                       |
| `sprite.pixelated`                                        | Crisp pixel scaling versus smooth scaling                                  |
| `sprite.fallbackLabel`                                    | Label shown if the sprite cannot load                                      |

Templates are displayed as plain text, not HTML. Keep placeholder spelling exact.
Use readable colors (for example #FFFFFF) and check long names/wrapping visually.
For 1920×1080 instead of the current 1980×1080, set canvasWidth to 1920 and positionX
to 960, preserve other properties, and match OBS dimensions.

### Animation, audio, and queue

| Property                                                            | Initial timing / meaning                                    |
| ------------------------------------------------------------------- | ----------------------------------------------------------- |
| `animation.usernameEnterMilliseconds`                               | 250; identity entrance duration                             |
| `animation.messageDelayMilliseconds`                                | 250; delay before message entrance                          |
| `animation.spriteDelayMilliseconds`                                 | 500; delay before sprite rise and cry attempt               |
| `animation.spriteRiseMilliseconds`                                  | 600; rise duration                                          |
| `animation.spriteRiseDistancePixels`                                | 120; distance below resting position, not the canvas bottom |
| `animation.holdMilliseconds`                                        | 4000 after entrance animations complete                     |
| `animation.exitMilliseconds`                                        | 400; exit fade                                              |
| `animation.bobEnabled`, `bobDistancePixels`, `bobCycleMilliseconds` | true, 6, 1200; gentle static-sprite movement                |
| `audio.enabled`                                                     | Enable/disable cries while retaining visuals                |
| `audio.volume`                                                      | 0–1; initially 0.5; OBS routing can change audible volume   |
| `audio.preferredCry`, `fallbackCry`                                 | `latest` / `legacy`, either key allowed                     |
| `audio.maxPlaybackMilliseconds`                                     | 5000; also stop audio when notification ends                |
| `queue.gapMilliseconds`                                             | 300 between completed notifications                         |

Media loading has a bounded wait before reveal. Message/sprite delays are relative to
notification reveal, and hold starts after all entrance animations finish. With current
timings the visual sequence is about 5.5 seconds plus 0.3-second gap, excluding media waits.
Keep delays ordered if you want identity → message → sprite. Increase bobCycleMilliseconds
for slower motion and reduce bobDistancePixels for subtler motion. Text stays stationary.

Missing avatars are hidden. Missing sprites show the label. Preferred cry is used when
present; fallback is selected only when the preferred reference is null. Playback failure
continues silently; the current implementation does not retry the alternate cry on failure.
A URL's presence does not prove its media works.

## Verification and limits

| Manual check         | Expected result                                     |
| -------------------- | --------------------------------------------------- |
| Normal preview       | Correct avatar/name/message/sprite, rise and bob    |
| Three rapid previews | Sequential notifications                            |
| Shiny preview        | Shiny wording/color/artwork                         |
| Audio disabled       | Animation continues silently                        |
| Portrait             | Readable, unclipped, clear of mobile controls       |
| Test log             | KapteinOle's real ID and isTest: true               |
| Missing image        | Label/hidden avatar; queue continues                |
| Server restart       | Reconnection; missed notifications are not replayed |

Accepted tests append one JSON object per line to runtime/overlay-tests.jsonl. Keep this
local data ignored. The endpoint rejects a request without a connected overlay client.
Acceptance means logged/broadcast, not acknowledged display or successful audio.

Queues are browser-memory only; refresh/close loses pending notifications. There is no
replay, production delivery deduplication, or durable outbox. Logs are not a delivery queue.
Production catches must be committed independently of presentation and must never be
rerolled because the overlay failed. That integration remains future work.

## Files and troubleshooting

| File                                        | Responsibility                              |
| ------------------------------------------- | ------------------------------------------- |
| `overlay/index.html`, `overlay/overlay.css` | Document/layout                             |
| `overlay/src/overlay.ts`                    | Browser queue, animation, media, connection |
| `overlay/tsconfig.json`                     | Browser build; DOM library and `types: []`  |
| `scripts/serve-overlay.ts`                  | Local server, endpoint, test log            |
| `streamerbot/tests/CatchOverlayCheck.cs`    | Manual sender                               |

Do not commit overlay/dist. Root TypeScript uses Node types; the browser config excludes
automatic Node globals to prevent undici-types errors. ESLint applies environment-appropriate globals.

| Symptom                                   | Fix                                                                     |
| ----------------------------------------- | ----------------------------------------------------------------------- |
| System.Net / InvalidDataException missing | Add framework System.dll in the action References tab                   |
| undici-types errors                       | Keep browser `types: []` and root `types: ["node"]`                     |
| No overlay connected                      | Start server, open source, inspect debug=1 label                        |
| User lookup fails                         | Run whole action; verify target kapteinole and sub-action order         |
| Duplicate cry                             | Mute extra browser instances; check monitoring/desktop-audio routing    |
| Changes absent                            | Rebuild changed TS, restart server, refresh browser; repaste changed C# |
