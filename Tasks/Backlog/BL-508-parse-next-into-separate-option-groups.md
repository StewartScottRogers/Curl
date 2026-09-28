---
id: BL-508
title: Parse -:/--next into separate option groups
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-488, BL-489, BL-491, BL-492, BL-493, BL-494, BL-495]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-508 — Parse -:/--next into separate option groups

## Goal

`-:`/`--next` ends one option group and starts a fresh one, so `CommandLineParser` returns an ordered list of groups, each with its own per-transfer options and URLs, while global options (curl's `-s`, `-v`, `--fail-early`, `--parallel` and the like) apply to all groups whichever group they appear in.

## Context

- Conformance audit 2026-09-28, row 8 (Blocker, M-L). Running the groups is BL-509.
- `Curl.Cli.UnitLibrary/CommandLineParser.cs` and `CommandLineParseResult.cs` currently produce one `CommandLineOptions`. `-K` config files may contain `next` lines too (`ConfigFileApplier.cs`).
- Which options are global is not guessable: curl marks them in its tool (`ARG_...` / the `global` flag). Measure the doubtful ones (for instance `-v` given only after `--next`, `-w` given only before it, `-o` counts per group, and a group with no URL: `curl URL --next`) with `Record-CurlExchange.ps1 -Connections 2`, and record the list of global options in Notes with how each was established.
- `-:` is accepted in a bundle position as curl allows; measure `-s:` too.
- Every option `CommandLineOptionTable` parses when this task runs is classified as global or per-group. It waits for BL-488, BL-489 and BL-491 to BL-495 so the options those add are classified here rather than left for someone to remember later; a test enumerating the option table and failing on an unclassified option keeps later additions honest.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: the cases above, stdout, stderr, request bytes and exit code copied into Notes.
- [ ] `CommandLineParseResult` exposes the groups in order; `Curl.Cli.UnitTests` show per-group options reset after `--next`, global options shared, and `-K` `next` lines splitting groups.
- [ ] A group with no URL is refused (or ignored) exactly as measured, with the text pinned.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
