---
id: BL-492
title: Parse --no-clobber and write to a numbered file name instead of overwriting
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-492 — Parse --no-clobber and write to a numbered file name instead of overwriting

## Goal

With `--no-clobber`, an `-o`/`-O` target that already exists is left alone and the body goes to the first free `<name>.1` … `<name>.100`, as curl 8.21.0 does; `--clobber` restores overwriting.

## Context

- Conformance audit 2026-09-28, row 11 (Blocker, S).
- Alias-table row `clobber`, `--no-` documented (`Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`); no row in `CommandLineOptionTable.cs`.
- Output files are opened in `Curl.Console` (`OutputFileTarget.cs`, `DeferredOutputFileStream.cs`, `IOutputPaths.cs`, `PhysicalOutputPaths.cs`). Use the existing `IOutputPaths` seam so tests need no disk.
- The manual text (`CurlManual.txt`, `--no-clobber`) states the numbering and the 100 limit; what curl prints and exits when all 100 exist must be measured.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (loopback 200, `-o out.txt --no-clobber`): target absent, target present, `out.txt` and `out.txt.1` present, and all of `out.txt`, `.1` … `.100` present; stdout, stderr, exit code and the files left behind copied into Notes.
- [ ] `--no-clobber` and `--clobber` parse (last wins); `Curl.Cli.UnitTests` covers both.
- [ ] `Curl.Console.UnitTests` tests through `IOutputPaths` pin each measured case: the file chosen, the untouched original, and the stderr and exit code when no name is free.
- [ ] `%{filename_effective}` reports the numbered name actually written.
- [ ] New tests are platform-neutral (no drive-letter paths).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
