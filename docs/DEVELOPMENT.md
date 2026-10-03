# Development Guide

This guide explains how to run and develop the project's tools.

## Current implementation status

The importer is written in TypeScript.

Its first milestone downloads and caches Pikachu source data.
It does not yet generate `data/pokemon.json`, create collection entries,
or connect to Streamer.bot.

The planned Streamer.bot catching action will use C#.

## Requirements

- Node.js 24 or newer.
- npm.
- Internet access for dependency installation and uncached API resources.

Check the installed versions:

```bash
node --version
npm --version
```

## Install dependencies

When setting up a checkout with the committed package lockfile, run:

```bash
npm ci
```

Development dependencies include:

- `typescript`: checks types and compiles the importer.
- `@types/node`: supplies TypeScript definitions for Node APIs.

Commit both `package.json` and `package-lock.json`.
Do not commit `node_modules`.

## Check types

```bash
npm run typecheck
```

This checks the TypeScript source without generating JavaScript.

Type checking does not validate downloaded API responses.
The importer also performs runtime checks on the fields it uses.

## Run the sample importer

From the project root:

```bash
npm run import:sample
```

This command:

1. Compiles the TypeScript files from `scripts/` into `dist/`.
2. Stops if compilation fails.
3. Runs the compiled importer.

Edit files inside `scripts/`. Files inside `dist/` are generated output
and should not be edited or committed.

## Downloaded resources

The first successful run downloads:

- `pokemon/pikachu`
- `pokemon-species/pikachu`

Responses are cached in:

```text
.cache/pokeapi/pokemon--pikachu.json
.cache/pokeapi/pokemon-species--pikachu.json
```

These are source API responses, not the game's catalogue format.

The terminal reports species information and whether sprite and cry
reference strings are present. It does not download or verify those
media files yet.

## Verify caching

Run the sample command again:

```bash
npm run import:sample
```

Both resources should report `[cache]` instead of `[download]`.

With both valid cache files and the development dependencies present,
this sample can run without downloading those resources again.

## Refresh a cached resource

Cached resources are retained until removed.

To fetch fresh data:

1. Stop any running importer.
2. Delete the relevant JSON file inside `.cache/pokeapi/`.
3. Run the sample command again.

Only missing resources are downloaded.

Do not edit cached responses to customize the game.
Reviewed catalogue overrides will be implemented separately.

## File responsibilities

| File                        | Responsibility                                            |
| --------------------------- | --------------------------------------------------------- |
| `package.json`              | Project metadata, dependencies, and npm commands          |
| `package-lock.json`         | Records the resolved dependency versions                  |
| `tsconfig.json`             | Type-checking and compilation settings                    |
| `scripts/import-pokemon.ts` | Coordinates the sample and validates its summary fields   |
| `scripts/lib/pokeapi.ts`    | Downloads, checks resource identity, and caches responses |
| `dist/`                     | Generated JavaScript; excluded from Git                   |
| `.cache/pokeapi/`           | Local API responses; excluded from Git                    |

## TypeScript conventions

- Treat external JSON as `unknown` until checked.
- Validate fields before using them.
- Use interfaces for validated structures.
- Do not use type assertions as a substitute for runtime validation.
- Keep comments focused on purpose and behavior.

Relative imports use `.js` extensions because the compiled files run
as Node ES modules.

For example, this TypeScript import:

```typescript
import { getPokeApiResource } from "./lib/pokeapi.js";
```

resolves to `scripts/lib/pokeapi.ts` during development and
`dist/lib/pokeapi.js` after compilation.

## Error handling

The importer stops with a non-zero exit code if reading, downloading,
parsing, validating required fields, or saving fails.

Network requests have a 15-second timeout.

Only a missing cache file triggers a download. Permission errors and
other file-reading failures are reported.

If a cache file contains invalid JSON or the wrong resource, remove
that specific file and run the importer again.

Missing sprite or cry references are reported as unavailable and do
not make this milestone fail.

## Validation limits

This milestone checks resource identity and the fields required for
its summary.

It does not yet enforce the complete catalogue rules in `DATA_MODEL.md`.

Full catalogue validation will be added alongside catalogue generation.

## Next milestone

Convert cached Pikachu source data into separate normal and shiny
collection entries for its indexed genders.

The importer will eventually generate the local catalogue used by
Streamer.bot. Live redemptions will not need to request Pokémon data
from PokéAPI.
