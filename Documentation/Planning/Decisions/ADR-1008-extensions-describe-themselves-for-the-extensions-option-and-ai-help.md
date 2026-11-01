# ADR-1008: Extensions describe themselves for `--extensions` and `--ai-help extensions`

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4093)

## Context

ADR-0999 keeps `curl -V` byte for byte upstream's, so the extensions Curl adds need a listing of their own.

## Decision

- Each extension supplies an `ExtensionDescription` (`Curl.Extension.Abstractions.Description`: name,
  summary, schemes, options, specification link) with one line in `Curl.Extension.Composition`'s
  `ExtensionDescriptionCatalogue`, a `merge=union` shared list file.
- `ExtensionListing` builds both outputs from them: `curl --extensions` prints one line per extension
  (`name: summary Schemes: ... Options: ...`), and `curl --ai-help extensions` (any case) prints a Markdown
  section per extension. With `CURL_EXTENSIONS=0` both say extensions are off; `--extensions` exits 0.
- `--extensions` is Curl's own option, not a curl option: the gap office ignores it. It ends parsing as
  `--help` does. It is in `--ai-help`'s Curl category and its index lists `extensions` beside the
  categories, but `--help` does not list it, so curl's help pages stay byte for byte (unlike `--ai-help`,
  whose row ADR-0224 put there so agents find it).
- `Curl.Cli` still references no extension project: the console asks `ExtensionListing` for the text.

## Consequences

Every later extension task adds a description line and is listed automatically.

## Amendment (2026-11-01, BL-4452)

Decided by Claude under Stewart's delegation, at Stewart's request ("In curl.Console change --extensions
to --extensions-help"). The option is now `--extensions-help`; `--extensions` is gone, and curl answers it
as any unknown option (exit 2). No alias is kept: the option is Curl's own, so no upstream script names it,
and a `-help` suffix says it prints help rather than changes a transfer, beside `--help` and `--ai-help`.
The `--ai-help extensions` subject and the `curl://ai-help/extensions` MCP resource keep their names: they
name a help subject, not the option. `--help` still leaves the option out, so curl's help pages stay byte
for byte; `--ai-help` lists it under the Curl category.
