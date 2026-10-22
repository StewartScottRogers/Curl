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
completed: 2026-10-19
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

- [x] This task is obsolete, and closes without a change: it duplicates BL-497, which is Done.

## Notes

- 2026-10-19: Stewart had it closed as obsolete. Its criteria were replaced by the one above while it was in the Backlog, and the evidence was checked mechanically first.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Deferred. Duplicate of BL-497, which implemented ADR-0137's parser change with the same acceptance criteria
- 2026-10-19: Deferred -> Backlog. Stewart: close it as obsolete (it duplicates BL-497, which is Done).
- 2026-10-19: Backlog -> Doing. Interactive session: closing it as obsolete. Criteria at claim: 9f42e37a.
- 2026-10-19: Doing -> Done. Obsolete: it duplicates BL-497, which is Done.
