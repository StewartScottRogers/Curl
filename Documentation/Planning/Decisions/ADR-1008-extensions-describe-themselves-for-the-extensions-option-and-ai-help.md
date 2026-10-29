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
