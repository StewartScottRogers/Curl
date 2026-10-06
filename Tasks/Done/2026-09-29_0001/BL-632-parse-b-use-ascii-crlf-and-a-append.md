---
id: BL-632
title: Parse -B/--use-ascii, --crlf and -a/--append
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-632 — Parse -B/--use-ascii, --crlf and -a/--append

## Goal

`-B`/`--use-ascii`, `--crlf` and `-a`/`--append` (with their `--no-` forms) parse into `CommandLineOptions`, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 24 (Major). FTP behaviour is BL-633.
- `--crlf` already has a consumer: `ITransferContext.ConvertLineEndings` and the `file://` handler (FR-013), which today cannot be reached from the command line; wire the option to it in `Curl.Console/TransferContextFactory.cs` only if that file is not in another running task's `touches` (BL-633 touches Console and will otherwise do it).

## Acceptance criteria

- [x] Every spelling, including `-B` and `-a` in bundles and the `--no-` forms, is covered by `Curl.Cli.UnitTests`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured curl 8.21.0 (Schannel, Windows) on 2026-09-29: `-B`, `--no-use-ascii`, `--crlf`, `--no-crlf`, `-a`, `--no-append` and the bundle `-sBa`, each with `file:///nonexist/zz`, all exit 37 (never 2), so every spelling is accepted and the three are `NegatableFlag` rows, matching `CurlOptionAliasTable`.
- New `CommandLineOptions` properties: `UseAscii`, `ConvertLineEndings` (the same name as `ITransferContext.ConvertLineEndings`, one concept one name) and `Append`.
- Classified per-group in `CommandLineNextGroupTests`: curl keeps `use_ascii`, `crlf` and `ftp_append` in each `OperationConfig`.
- Default taken: the `Curl.Console/TransferContextFactory.cs` wiring of `--crlf` is left to BL-633, which touches `Curl.Console` and already names it in its criteria; this task stays inside its `touches`.
- Tests: `Curl.Cli.UnitTests/CommandLineAsciiCrlfAndAppendOptionTests.cs` (9 cases). Cli tests 2846 passed, 13 skipped; `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. -B/--use-ascii, --crlf and -a/--append (and --no- forms, bundles) parse into CommandLineOptions
