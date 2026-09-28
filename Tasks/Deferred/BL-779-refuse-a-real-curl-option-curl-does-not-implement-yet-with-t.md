---
id: BL-779
title: Refuse a real curl option Curl does not implement yet with 'the installed libcurl version does not support this' (ADR-0137)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-496]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-779 — Refuse a real curl option Curl does not implement yet with 'the installed libcurl version does not support this' (ADR-0137)

## Goal

A long name, short letter or `-K` config option that is in `CurlOptionAliasTable` but has no `CommandLineOptionTable` row is refused with `curl: option <spelled>: the installed libcurl version does not support this` and exit 2, as ADR-0137 decides, while a name in neither table keeps `is unknown`.

## Context

- ADR-0137 (`Documentation/Planning/Decisions/ADR-0137-a-real-curl-option-not-yet-implemented-is-refused-as-the-installed-libcurl-does-not-support-it.md`) is the specification: its Decision table and the class-3 details (`--no-<name>`, `--expand-<name>`, short letters, `-K` lines, `--help <option>`).
- Today every such option reaches `CommandLineRefusal.UnknownOption` from `CommandLineParser.ParseNegatedLong`, `ParseExpandedLong` and `ApplyBundleLetterEndsBundle`. `CommandLineRefusal.InstalledLibcurlDoesNotSupport` already builds the class-3 line (used by `CommandLineOption.UnsupportedFlag`), and `ConfigFileOptionRefused` already carries a non-unknown reason through to the `-K` lines.
- The unimplemented set is alias table minus option table, computed, never a hand-written list. 93 names on 2026-09-28, among them `--ipv4`/`-4`, `--ipv6`/`-6`, `--netrc`/`-n`, `--use-ascii`/`-B`.
- Measured shapes (Windows system curl 8.21.0, 2026-09-28) are in the ADR's Context.

## Acceptance criteria

- [ ] A test shows `--ipv4` (while unparsed) is refused with `curl: option --ipv4: the installed libcurl version does not support this` and the try-help line, exit 2, and `--ipv4x` still with `is unknown`.
- [ ] A test shows a bundle containing an unimplemented letter (e.g. `-s4`) is refused with the class-3 line spelled as the whole argument.
- [ ] Tests show `--no-<name>` of an unimplemented option gives `cannot be reversed with a --no- prefix` when the alias table says `NotAccepted`, else the class-3 line; and `--expand-<name>` gives the class-3 line.
- [ ] A test shows a `-K` file line naming an unimplemented option prints `config file option '<option>' the installed libcurl version does not support this` then `curl: option -K: the installed libcurl version does not support this`, exit 2.
- [ ] A test enumerates `CurlOptionAliasTable.Aliases` and asserts every name either has a `CommandLineOptionTable` row or is refused with the class-3 line, never `is unknown`.
- [ ] `dotnet build` clean, fast tests green, `Curl.Cli.UnitLibrary` at 100% line and branch coverage.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Deferred. Duplicate of BL-497, which implemented ADR-0137's parser change with the same acceptance criteria
