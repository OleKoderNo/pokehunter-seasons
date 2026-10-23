# Development Guide

This guide covers Node/TypeScript tooling and catalogue generation. Use
[C# Development](CSHARP_DEVELOPMENT.md) for C# builds/tests and
[Catch Overlay](CATCH_OVERLAY.md) for OBS preview setup.

## Setup

Requirements: Node.js 24+, npm, and internet access for installation/uncached resources.
Run all commands from the repository root.

```powershell
node --version
npm --version
npm ci
```

Use the committed lockfile. TypeScript is pinned to 5.9.3; review its compatibility with
typescript-eslint before upgrading. Commit package.json and package-lock.json together
when both change. Never commit node_modules.

Edit `scripts/**/*.ts` for Node tools and `overlay/src/**/*.ts` for the browser.
The builds produce `dist/` and `overlay/dist/`; do not edit or commit generated JavaScript.
Relative TypeScript imports use `.js` extensions because the compiled output runs as
Node ES modules. Browser TypeScript has a separate config with DOM types and `types: []`.

## Everyday commands

| Command                                   | Purpose                                                                         |
| ----------------------------------------- | ------------------------------------------------------------------------------- |
| `npm run check`                           | Node/browser type checks, ESLint, compiled tooling tests, then formatting check |
| `npm run test:tools`                      | Compile and run offline catalogue-tool tests                                    |
| `npm run typecheck`                       | Check Node tooling without emitting JavaScript                                  |
| `npm run typecheck:overlay`               | Check browser TypeScript without emitting                                       |
| `npm run build:tools`                     | Compile Node tools                                                              |
| `npm run build:overlay`                   | Compile browser overlay                                                         |
| `npm run lint` / `npm run lint:fix`       | Report issues / apply supported fixes                                           |
| `npm run format` / `npm run format:check` | Format supported files / check without edits                                    |
| `npm run overlay:serve`                   | Build tools and overlay, then run the local preview server                      |

`npm run check` does not build C#, run API-backed importers, or validate generated
catalogue files. Its test step emits JavaScript in dist. Prettier does not format C#.
ESLint covers configured Node tooling, the TypeScript ESLint config, and browser source.

## Generate the current catalogue

Run in this order, stopping if any command fails. Each importer/inspection command
builds the tools before running its script.

```powershell
npm run import:sample
npm run import:regular-sample
npm run import:gen1
npm run import:event-families
npm run review:event-types
npm run inspect:event-forms
npm run import:reviewed-ghosts
npm run inspect:event-variants
npm run import:event-variants
npm run merge:catalogue
```

| Stage                       | What it does                                                                   |
| --------------------------- | ------------------------------------------------------------------------------ |
| Samples                     | Generate Pikachu and representative gender-case examples; compare converters   |
| Generation 1                | Generate regular entries and verify species coverage                           |
| Event families              | Resolve all evolution branches and generate regular family members             |
| Type review                 | Discover candidates matching Halloween's type selectors                        |
| Form inspection             | Record form metadata for review before conversion                              |
| Reviewed Ghost import       | Convert reviewed forms; compare overlap against Generation 1                   |
| Explicit variant inspection | Print metadata for Hangry Morpeko and five costumes; does not generate entries |
| Explicit variant import     | Generate two Morpeko and ten female-only costume entries                       |
| Merge                       | Validate, deduplicate, check counterparts, build groups, and apply rarity      |

The reviewed Ghost import depends on the form-details report and Generation 1 output.
The workflow currently targets `halloween-2026`; changing an event ID alone does not
make its scripts support another event. New ambiguous form mappings require review.

An old output can remain after a failed command. Its existence is not evidence that
the latest run succeeded. Regenerate affected downstream outputs after source changes.

## Output reference

| Path                                              | Contents                                    |
| ------------------------------------------------- | ------------------------------------------- |
| `data/samples/pikachu.json`                       | Four regular Pikachu entries                |
| `data/samples/regular-species.json`               | Representative regular species              |
| `data/catalogue/generation-1.json`                | Regular Generation 1 entries                |
| `data/catalogue/halloween-2026-families.json`     | Family selections and regular members       |
| `data/catalogue/halloween-2026-type-review.json`  | Type-discovered candidates                  |
| `data/catalogue/halloween-2026-form-details.json` | Reviewed form source metadata               |
| `data/catalogue/halloween-2026-ghosts.json`       | Reviewed Ghost entries                      |
| `data/catalogue/halloween-2026-variants.json`     | Hangry Morpeko and ORAS costumes            |
| `data/catalogue/merged-entries.json`              | Combined entries and rated encounter groups |

Existing tracked samples remain examples. Everything under data/catalogue is ignored;
do not force-add it. Final production packaging is not implemented.

The verified merge has 632 input entries, 12 identical overlaps removed, 620 entries,
and 277 groups. It remains `sampleOnly: true`. See [Data Model](DATA_MODEL.md).

## Merge behavior

The merger reads the four generated entry catalogues and `config/rarity.json` locally;
it does not fetch API resources. Entry validation rejects malformed records, duplicate
IDs within a source, conflicting cross-source copies, and inconsistent counterparts.
Group validation rejects inconsistent species references. Rarity overrides must use
known group IDs and one of the four supported classes.

All validation precedes output replacement. Temporary-file replacement protects the
previous merged file from validation failure; the entire multi-command import workflow
is not transactional. Correct problems upstream, never by editing generated JSON.

## Cache and media

PokéAPI responses are stored in `cache/`, for example:

```text
cache/pokemon--pikachu.json
cache/pokemon-species--pikachu.json
cache/evolution-chain--10.json
```

The terminal reports `[download]` or `[cache]`. With all required valid responses cached,
commands can reuse them without downloading. The cache is not automatically refreshed
or a versioned source snapshot.

To refresh a resource: stop the importer, delete only its cache file, then rerun the
relevant command. Only missing files trigger downloads. Invalid JSON/resource identity,
permission errors, and other read failures are reported. Requests have a 15-second timeout.

Media checks validate references, not every remote file. Null artwork/audio must not
remove an otherwise valid entry. A present URL does not prove that it loads or matches
the correct form, gender, or shiny state.

## Where to change code

| Location/module                                                                                 | Responsibility                                  |
| ----------------------------------------------------------------------------------------------- | ----------------------------------------------- |
| `scripts/types/catalogue.ts`                                                                    | Entry/group/rarity contracts                    |
| `scripts/lib/pokeapi.ts`                                                                        | Resource access, identity checks, cache         |
| `read-evolution-chain-id.ts`, `collect-evolution-species.ts` in lib                             | Family references/traversal                     |
| `create-pikachu-entries.ts`, `create-regular-entries.ts` in lib                                 | Sample and regular conversion                   |
| `create-form-entries.ts`, `create-event-variant-entries.ts` in lib                              | Reviewed forms/costumes                         |
| `validate-collection-entry.ts`, `validate-entry-counterparts.ts` in lib                         | Entry and counterpart validation                |
| `merge-collection-entries.ts`, `create-encounter-groups.ts`, `apply-encounter-rarity.ts` in lib | Merge, grouping, rarity resolution              |
| `scripts/import-*.ts`, `review-*.ts`, `inspect-*.ts`                                            | Workflow coordination/reports                   |
| `scripts/merge-catalogue.ts`                                                                    | Read, validate, merge, and save current outputs |
| `scripts/serve-overlay.ts`                                                                      | Local overlay server and test endpoint          |
| `scripts/tests/`                                                                                | Offline behavior checks and fixtures            |
| `package.json`, `tsconfig.json`, `eslint.config.mts`                                            | Commands, compilation, linting                  |

## Testing and contribution workflow

Offline tests include catalogue conversion, event variants, merging, entry validation,
encounter groups, and rarity assignments. Keep all existing test files in `test:tools`
when adding new checks. Tests use fresh fixtures; they do not require API/cache access.

Importer assertions separately check real cached/downloaded data: representative genders,
Generation 1 coverage, reviewed forms, counterpart consistency, and overlapping outputs.
Inspection reports alone do not prove conversion correctness. Filesystem failure paths,
downloader behavior, and every remote asset are not exhaustively covered.

Before committing:

1. Run behavior checks for changed code and regenerate affected catalogues when needed.
2. Run `npm run format`, then `npm run check`.
3. Run relevant [C# checks](CSHARP_DEVELOPMENT.md#standalone-checks) for C# or contract changes.
4. Use the overlay's manual checklist when presentation behavior changes.
5. Review `git diff --check`, `git status --short`, and the diff.
6. Update the relevant guide and stage only intended changes.

Treat external JSON as unknown until validated; type assertions are not validation.
Use type-only imports, preserve stable IDs, and explain non-obvious rules in comments.
Keep fetching, conversion, and file writing separate. Preserve regression checks when
refactoring. Do not disable lint rules merely to hide unexplained errors.

## Remaining integration

The current catalogue can be loaded in C# and used to build eligible categories.
Deterministic weighted selection is available separately. A random-number source,
complete encounter selection, viewer-specific ownership filtering, catches/redemptions, controlled
runtime loading/reloading, and production catalogue packaging remain. See
[Architecture](ARCHITECTURE.md) for boundaries rather than treating imports as a live game.
