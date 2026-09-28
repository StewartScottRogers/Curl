---
id: BL-494
title: Parse --remove-on-error and delete the output file of a failed transfer
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-494 — Parse --remove-on-error and delete the output file of a failed transfer

## Goal

With `--remove-on-error`, a transfer that fails removes the `-o`/`-O` file it wrote, as curl 8.21.0 does; a successful transfer, and standard output, are unaffected.

## Context

- Conformance audit 2026-09-28, row 11 (Blocker, S).
- Alias-table row `remove-on-error`, `--no-` accepted (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`); no row in `CommandLineOptionTable.cs`.
- Output files are managed in `Curl.Console` (`OutputFileTarget.cs`, `DeferredOutputFileStream.cs`) behind `IOutputPaths`; the failure point is the transfer result in `CurlCommandRunner.cs`.
- Whether curl also removes the file for `-f` (exit 22) and for a partial transfer (exit 18), and what it does with a file that existed before the transfer, must be measured.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--remove-on-error -o out.txt` for a 200, a 404 with `-f`, a body cut short (exit 18), and with `out.txt` existing beforehand; files left behind, stderr and exit code copied into Notes.
- [ ] `--remove-on-error` and `--no-remove-on-error` parse; `Curl.Cli.UnitTests` covers both.
- [ ] `Curl.Console.UnitTests` tests through `IOutputPaths` pin each measured case, including that the exit code and message are those of the failure.
- [ ] New tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
