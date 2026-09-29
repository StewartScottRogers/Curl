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
completed:
---
# BL-632 — Parse -B/--use-ascii, --crlf and -a/--append

## Goal

`-B`/`--use-ascii`, `--crlf` and `-a`/`--append` (with their `--no-` forms) parse into `CommandLineOptions`, instead of `is unknown` and exit 2.

## Context

- Conformance audit 2026-09-28, row 24 (Major). FTP behaviour is BL-633.
- `--crlf` already has a consumer: `ITransferContext.ConvertLineEndings` and the `file://` handler (FR-013), which today cannot be reached from the command line; wire the option to it in `Curl.Console/TransferContextFactory.cs` only if that file is not in another running task's `touches` (BL-633 touches Console and will otherwise do it).

## Acceptance criteria

- [ ] Every spelling, including `-B` and `-a` in bundles and the `--no-` forms, is covered by `Curl.Cli.UnitTests`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
