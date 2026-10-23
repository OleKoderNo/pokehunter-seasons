# Development Guide

This guide explains how to develop and run the TypeScript catalogue
tools for PokéHunter Seasons.

For C# projects, Streamer.bot action checks, and local DLL deployment,
see [C# Development Setup](CSHARP_DEVELOPMENT.md).

For SQLite setup and storage behaviour, see
[SQLite Setup](SQLITE_SETUP.md) and [Database](DATABASE.md).

## Current implementation status

The TypeScript tools can:

- Generate a four-entry regular Pikachu sample.
- Check the general regular-species converter against representative species.
- Generate regular Generation 1 collection entries.
- Resolve event family selectors into complete evolution-chain species lists.
- Generate regular entries for those family members.
- Discover Pokémon resources matching event type selectors.
- Inspect form metadata before conversion.
- Generate the reviewed Ghost-type collection entries.
- Inspect Hangry Morpeko and the five selected Pikachu costumes.

These tools produce samples, intermediate catalogues, and review reports.

They do not yet produce a complete release-ready game catalogue.
They do not execute catches, write player ownership, or activate events
inside Streamer.bot.

Hangry Morpeko and the five costume resources have been inspected.
Their dedicated conversion and integration are still pending.

The C# configuration and SQLite foundations are implemented separately.
Catalogue integration with the catching runtime is still pending.

## Requirements

- Node.js 24 or newer.
- npm.
- Internet access for installing dependencies and downloading uncached
  PokéAPI resources.

Check your installed versions:

```powershell
node --version
npm --version
```

Run the commands in this guide from the repository root.

## Install dependencies

For a checkout containing the committed lockfile:

```powershell
npm ci
```

Use the project-local dependency versions recorded in
`package-lock.json`.

The development tools include:

- TypeScript for type checking and compilation.
- Node.js type definitions.
- ESLint and typescript-eslint for code-quality checks.
- eslint-config-prettier to disable conflicting formatting rules.
- Prettier for supported source and documentation formatting.
- jiti for loading the TypeScript ESLint configuration.

TypeScript is currently pinned to `5.9.3`.
Review TypeScript and typescript-eslint compatibility together before
upgrading.

Commit `package.json` and `package-lock.json` together when dependency
changes affect both files.

Do not commit `node_modules/`.

## Source and compiled files

Edit TypeScript files inside `scripts/`.

The build command compiles them into `dist/`:

```powershell
npm run build:tools
```

Files in `dist/` are generated JavaScript.
Do not edit or commit them.

Relative imports in TypeScript use `.js` extensions because the compiled
files execute as Node ES modules:

```typescript
import { getPokeApiResource } from "./lib/pokeapi.js";
```

TypeScript resolves this to the source module during development.
Node resolves the generated JavaScript module after compilation.

Each importer or inspection npm command builds the tools before
executing its script. If compilation fails, that script does not run.

## Available commands

| Command                          | Purpose                                                                       |
| -------------------------------- | ----------------------------------------------------------------------------- |
| `npm run typecheck`              | Check TypeScript without emitting JavaScript                                  |
| `npm run build:tools`            | Compile the TypeScript tools                                                  |
| `npm run import:sample`          | Generate the original four-entry Pikachu sample                               |
| `npm run import:regular-sample`  | Run regular-species sample checks and generate their sample                   |
| `npm run import:gen1`            | Generate regular Generation 1 entries                                         |
| `npm run import:event-families`  | Resolve the selected event's families and generate regular entries            |
| `npm run review:event-types`     | Discover candidates matching the selected event's types                       |
| `npm run inspect:event-forms`    | Inspect the discovered candidates' form metadata                              |
| `npm run import:reviewed-ghosts` | Generate entries from the reviewed Halloween Ghost forms                      |
| `npm run inspect:event-variants` | Print metadata for Hangry Morpeko and the five selected costumes              |
| `npm run lint`                   | Check configured source files with ESLint                                     |
| `npm run lint:fix`               | Apply supported ESLint fixes                                                  |
| `npm run format`                 | Format files covered by Prettier                                              |
| `npm run format:check`           | Check formatting without changing files                                       |
| `npm run check`                  | Run type checking, linting, standalone converter tests, and formatting checks |
| `npm run test:tools`             | Compile and run the standalone converter tests without API or cache access    |

The current event workflow targets `halloween-2026`.
Some scripts still contain Halloween-specific paths and reviewed
expectations.

Do not assume that changing an event ID alone makes every script
support a different event.

## Generate the current samples and intermediate catalogues

Start with the sample checks:

```powershell
npm run import:sample
npm run import:regular-sample
```

Then generate Generation 1 and the event families:

```powershell
npm run import:gen1
npm run import:event-families
```

Run the type discovery, form inspection, and reviewed import in order:

```powershell
npm run review:event-types
npm run inspect:event-forms
npm run import:reviewed-ghosts
```

The reviewed Ghost importer requires:

- The form-details report produced by `inspect:event-forms`.
- The Generation 1 catalogue produced by `import:gen1`.

It compares overlapping entries against Generation 1 so the same
collection identity is not represented differently by two importers.

Inspect the explicit event variants separately:

```powershell
npm run inspect:event-variants
```

This last command prints a report.
It does not generate collection entries or change catalogue files.

Stop and investigate a failed command before proceeding to a command
that depends on its output.

An older output file may still exist after a failed run.
Its presence does not prove the latest import succeeded.

## Generated outputs

| Path                                              | Contents                                              |
| ------------------------------------------------- | ----------------------------------------------------- |
| `data/samples/pikachu.json`                       | Four regular Pikachu collection entries               |
| `data/samples/regular-species.json`               | Representative regular-species sample entries         |
| `data/catalogue/generation-1.json`                | Regular Generation 1 entries                          |
| `data/catalogue/halloween-2026-families.json`     | Selected evolution families and their regular entries |
| `data/catalogue/halloween-2026-type-review.json`  | Candidates discovered through type selectors          |
| `data/catalogue/halloween-2026-form-details.json` | Detailed form inspection results                      |
| `data/catalogue/halloween-2026-ghosts.json`       | Reviewed Ghost-type collection entries                |

The catalogue files are intermediate build products.
Their wrappers and review fields are not the final runtime catalogue
contract.

Keep existing tracked samples as reviewable examples.
Regenerate them through the tools rather than editing their JSON
manually.

Generated files under `data/catalogue/` and downloaded responses under
`cache/` are excluded from Git.

Do not force-add these ignored directories.
A future release workflow must package the validated runtime catalogue
or provide documented generation steps.

## Downloaded resources and caching

The downloader stores API responses in the repository's `cache/`
directory.

Examples:

```text
cache/pokemon--pikachu.json
cache/pokemon-species--pikachu.json
cache/evolution-chain--10.json
```

These files contain source API responses, not collection entries.

The terminal reports:

- `[download]` when fetching a missing resource.
- `[cache]` when using an existing cached resource.

Run a command a second time to confirm that its resources are reused.

A command can avoid network requests when all of its required resources
are already cached and its dependencies are installed.

The cache is not automatically refreshed.
It is also not a versioned snapshot of PokéAPI.

### Refresh a resource

1. Stop any importer using the cache.
2. Delete the specific resource file from `cache/`.
3. Run the relevant command again.

Only a missing cache file triggers a download.

Invalid JSON, unexpected resource identity, permission errors, and
other read failures are reported instead of being silently replaced.

Do not edit cached responses to customize the game.
Game-specific decisions belong in reviewed conversion rules or future
override configuration.

## File responsibilities

| File                                       | Responsibility                                                 |
| ------------------------------------------ | -------------------------------------------------------------- |
| `scripts/types/catalogue.ts`               | Defines the current collection-entry TypeScript types          |
| `scripts/lib/pokeapi.ts`                   | Fetches, validates resource identity, and caches API responses |
| `scripts/lib/read-evolution-chain-id.ts`   | Validates and extracts evolution-chain references              |
| `scripts/lib/collect-evolution-species.ts` | Collects species across evolution-chain branches               |
| `scripts/lib/create-pikachu-entries.ts`    | Generates the original Pikachu reference sample                |
| `scripts/lib/create-regular-entries.ts`    | Converts supported regular species and exposes shared readers  |
| `scripts/lib/create-form-entries.ts`       | Converts reviewed forms, including special gender mappings     |
| `scripts/import-pokemon.ts`                | Coordinates the original Pikachu sample                        |
| `scripts/import-regular-sample.ts`         | Runs representative regular-species checks                     |
| `scripts/import-generation-one.ts`         | Generates and checks regular Generation 1 entries              |
| `scripts/import-event-families.ts`         | Resolves family selectors and generates regular family entries |
| `scripts/review-event-types.ts`            | Discovers event type candidates for review                     |
| `scripts/inspect-event-forms.ts`           | Inspects candidate forms and their introduction metadata       |
| `scripts/import-reviewed-ghosts.ts`        | Generates and checks the reviewed Ghost entries                |
| `scripts/inspect-event-variants.ts`        | Inspects the six explicit event variant resources              |
| `eslint.config.mts`                        | Configures ESLint                                              |
| `tsconfig.json`                            | Configures TypeScript compilation                              |
| `package.json`                             | Defines dependencies and npm commands                          |
| `package-lock.json`                        | Records resolved dependency versions                           |

## Testing and validation

Testing applies to both TypeScript and C#.

Different checks provide different kinds of evidence.

### Combined development checks

Run:

```powershell
npm run check
```

This runs the following checks in order:

1. TypeScript type checking.
2. ESLint.
3. Compilation and standalone converter tests.
4. Prettier's formatting check.

The command stops when a step fails.

It runs standalone converter tests, but does not run the API-backed
importers or validate generated catalogue files.

The test step generates compiled JavaScript in `dist/`.
It does not modify source files, download Pokémon data, or write
collection catalogues.

A successful type check does not establish that external JSON is valid.
Runtime readers must still validate the fields they use.

Run the relevant importer behaviour checks separately when changing
conversion or catalogue-generation logic.

### Existing importer behaviour checks

Several import scripts contain assertions.

Current coverage includes:

- Evolution-chain reference validation.
- Expected entries for representative gender cases.
- Agreement between the original Pikachu converter and the general converter.
- Generation 1 species coverage and unique collection identities.
- Normal and shiny counterparts.
- Reviewed form metadata and special gender identities.
- Agreement between overlapping Generation 1 and Ghost entries.

Run the relevant importer commands when changing those behaviours.

These checks use downloaded or cached source data.
Results can therefore depend on the available API data and local cache.

Inspection scripts primarily report metadata.
A successful inspection is not proof that conversion or encounter rules
are correct.

### Standalone TypeScript tests

Run:

```powershell
npm run test:tools
```

The command compiles the TypeScript tools and runs
`dist/tests/catalogue.test.js` using Node's built-in test runner.

Test source is stored in `scripts/tests/catalogue.test.ts`.
Fresh synthetic fixtures come from
`scripts/tests/fixtures/catalogue-fixtures.ts`.

The tests do not download API resources, read the cache, or write
catalogues. Fixture media URLs are placeholders and are never requested.

Coverage includes:

- Normal and shiny identities.
- Visible gender differences.
- Genderless, single-gender, and unspecified entries.
- Missing optional media and malformed sprite protocols.
- Invalid required metadata and duplicate types.
- Regional-form encounter groups and introduction generations.
- Form types taking precedence over parent types.
- Canonical gender mappings for Frillish, Jellicent, and Basculegion.
- Matching parent sprite fallback.
- Preventing alternate forms from borrowing default-form artwork.
- Unsupported form labels, generations, and mismatched resources.

The importer checks remain in place to verify conversion against
downloaded or cached source data.

These tests do not cover the downloader, final catalogue merging,
costume conversion, or runtime encounter selection.

### C# checks

The npm commands do not build or test C#.

Follow [C# Development Setup](CSHARP_DEVELOPMENT.md) for configuration
checks, Streamer.bot actions, and storage checks.

Run the relevant C# checks when changing C# code or integrating a changed
catalogue contract with the runtime.

## Media references

The tools read sprite and cry references from source data.

A non-null URL means a reference is present.
It does not prove that the remote file exists, loads successfully, or
contains the correct visual or audio content.

The current workflow does not download and verify all media assets.

Missing optional references use `null`.
They must not silently remove collection entries.

Malformed references are rejected by the conversion readers.

## Error handling

Import commands report failures and set a non-zero exit code.

Network requests have a 15-second timeout.

Failures can include:

- Unreadable or malformed cache files.
- Failed requests.
- Unexpected API resource identity.
- Missing or invalid required fields.
- Unsupported reviewed mappings.
- Duplicate or inconsistent collection identities.
- Failed output writes.

Read the reported cause before deleting files or changing validation.

The catalogue generators that use temporary-file replacement preserve
the previous output until the new file has been written successfully.
This does not make the entire multi-command workflow transactional.

## Code quality and formatting

Run:

```powershell
npm run format
npm run check
```

Review the diff after formatting.

For supported automatic lint fixes:

```powershell
npm run lint:fix
```

Some lint problems require manual changes.
Do not disable rules simply to hide an unexplained error.

ESLint covers the TypeScript scripts and its TypeScript configuration
according to `eslint.config.mts`.

Prettier handles supported files, including TypeScript, JSON, and Markdown,
subject to the project's ignore configuration.

Prettier does not format C#.
See the C# development guide for the C# formatter.

Editor extensions are optional.
The command-line checks provide the shared workflow for contributors.

## TypeScript conventions

- Treat external JSON as `unknown` until validated.
- Validate required fields before using them.
- Use types to describe validated structures.
- Do not use a type assertion as a substitute for runtime validation.
- Use type-only imports where required by ESLint.
- Keep collection identity separate from display text.
- Keep conversion rules separate from downloading and file writing.
- Explain non-obvious decisions in comments.
- Preserve existing checks when adding new coverage.

## Before committing

1. Run the behaviour checks relevant to the changed code.
2. Run `npm run format`.
3. Run `npm run check`.
4. Run `git diff --check`.
5. Review `git status --short` and the diff.
6. Update documentation when commands, outputs, or behaviour change.
7. Stage only the intended source, sample, and documentation changes.

Do not commit cached responses, compiled output, local installation
settings, player databases, or backups.

Passing formatting and lint checks does not replace reviewing what is
being committed.

## Remaining catalogue work

Before the catalogue can support live catches, we still need to:

- Add standalone converter tests.
- Convert and validate the explicit event forms and costumes.
- Resolve outstanding form and encounter-group mappings.
- Merge overlapping generated entries without conflicting identities.
- Review rarity assignments.
- Build and validate the final runtime catalogue.
- Validate configured event selectors against that catalogue.
- Integrate catalogue loading and eligibility with C#.
- Document generation, packaging, and deployment for other creators.
- Add standalone converter tests.

See [Pokémon Data Model](DATA_MODEL.md) for the current entry contract,
planned runtime structure, and known differences.
