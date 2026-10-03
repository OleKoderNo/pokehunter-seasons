# Development Guide

This guide explains how to run and develop the project's tools.

## Current implementation status

The importer is written in TypeScript.

It downloads or reads cached Pikachu source data and generates four
regular Pikachu collection entries:

- Normal male.
- Normal female.
- Shiny male.
- Shiny female.

The output is saved to `data/samples/pikachu.json`.

This is an entry sample, not the complete catalogue described in
`DATA_MODEL.md`. It does not contain the species, family, or encounter
group records required by the full game.

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

## Generated Pikachu sample

Run:

`npm run import:sample`

The command compiles the tools, reads or downloads the source data,
and replaces `data/samples/pikachu.json`.

The generated sample contains four permanent collection IDs:

- `pikachu:male:normal`
- `pikachu:female:normal`
- `pikachu:male:shiny`
- `pikachu:female:shiny`

Each entry contains its gender, shiny state, types, form metadata,
and available sprite and cry references.

Missing media references are stored as `null`.
Entries are not removed because artwork is missing.

The converter checks required source fields and rejects malformed
media references. It does not download images or audio to verify
that the referenced files are reachable.

The sample output should be committed as a reviewable example.
Do not edit it manually; regenerate it through the importer.

### Additional source files

| File                                    | Responsibility                                        |
| --------------------------------------- | ----------------------------------------------------- |
| `scripts/types/catalogue.ts`            | Defines the collection entry structure                |
| `scripts/lib/create-pikachu-entries.ts` | Converts regular Pikachu into four collection entries |

## Next milestone

Expand source parsing and generation beyond regular Pikachu.

The full catalogue will also include species records, complete evolution
families, encounter groups, and validation of references between them.

## Code quality and formatting

The project uses:

- TypeScript for static type checking.
- ESLint with typescript-eslint for code-quality checks.
- Prettier for consistent source and documentation formatting.
- EditorConfig for shared editor settings.

These are development tools. They do not run during streams.

### Install dependencies

After cloning the repository, run:

```bash
npm ci
```

Use the project-local tool versions recorded in `package-lock.json`.

TypeScript is currently pinned to `5.9.3` for compatibility with the
selected typescript-eslint release. Review their supported versions
together before upgrading.

Commit `package.json` and `package-lock.json` together when dependencies
change.

### Check the project

```bash
npm run check
```

This runs type checking, linting, and formatting checks in order.
It stops when a check fails.

The command does not modify source files or download Pokémon data.

These checks do not replace runtime tests or catalogue validation.

### Format files

```bash
npm run format
```

This formats supported source files, configuration, and documentation.

Generated catalogues, cached responses, compiled output, player data,
and backups are excluded.

Review the Git diff after formatting.

To check formatting without changing files:

```bash
npm run format:check
```

### Lint source code

```bash
npm run lint
```

To apply supported automatic fixes:

```bash
npm run lint:fix
```

Review automatic changes before committing. Some problems require
manual fixes.

Do not disable rules just to hide an unexplained error.

### Editor integration

VS Code users can optionally install:

- ESLint, published by Microsoft.
- Prettier - Code formatter, published by Prettier.

Use the project's installed Prettier version and configuration.

Editor extensions are optional. The npm commands provide the shared
checks for everyone working on the project.

### Tool coverage

ESLint currently checks TypeScript files inside `scripts/` and root
`.mjs` configuration files.

Prettier formats supported handwritten files, including JSON and Markdown.

C# formatting, analysis, and compilation checks will be configured
when the Streamer.bot implementation is added.

### Before committing

1. Run `npm run lint:fix`.
2. Run `npm run format`.
3. Run `npm run check`.
4. Review the changes.
5. Run relevant behavior checks for any code you changed.
