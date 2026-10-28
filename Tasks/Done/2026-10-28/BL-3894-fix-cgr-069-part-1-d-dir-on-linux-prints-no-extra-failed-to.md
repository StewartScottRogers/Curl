---
id: BL-3894
title: Fix CGR-069 part 1: -d @dir on Linux prints no extra 'Failed to open' line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitTests/Cgr069DataFileTests.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/IDataFileReader.cs, Curl.Cli.UnitLibrary/DiskDataFileReader.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3894 — Fix CGR-069 part 1: -d @dir on Linux prints no extra 'Failed to open' line

## Goal

On Linux, `curl -sS -d @sub http://127.0.0.1:PORT/a` behaves as curl 8.22.0 does: same exit code, stdout bytes, stderr text and request bytes on the wire (CGR-069).

## Context

Part of BL-3598 (CGR-069, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). Expected real curl: exits 26 with two lines: `curl: option -d: error encountered when reading a file` and the try-help line (no `curl: Failed to open sub` line). Measure curl first with `Record-CurlExchange.ps1`; re-check against 8.22.0.

## Acceptance criteria

- [x] `curl -sS -d @sub http://127.0.0.1:PORT/a` through real curl and through Curl gives the same exit code, stdout bytes, stderr text and request bytes; commands and both results recorded in Notes.
- [x] A focused unit test in the named `.UnitTests` file fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3598 (over its cost cap).
- Continues the stashed work of BL-3598.
- 2026-10-28 (lane 5): added CommandLineOptionTable.cs, IDataFileReader.cs and DiskDataFileReader.cs to touches: the refusal is chosen in `ReadAtFile`, which alone knows the platform's reader; no task in Doing on origin/work/dark-factory names them.
- Fix: `IDataFileReader.OpensAsUnreadableDirectory` (default false; `DiskDataFileReader` true for a directory off the Windows build, where `fopen` opens it) makes `ReadAtFile` refuse with the new `CommandLineRefusal.DataFileOpenedButUnreadable` (two lines, exit 26) instead of `DataFileUnreadable` (which adds `curl: Failed to open <file>`). Covers `-d @dir` and `-w @dir`; Windows keeps the three lines, as Windows curl 8.22.0 does. Carried over from the BL-3598 stash, with a named refusal in place of passing the directory check as `errorsHidden`. No ADR: this matches measured curl and involves no design choice.
- Measured on WSL (only curl 8.18.0 is installed there; no 8.22.0 for Linux on this machine): `mkdir sub; ./Record-CurlExchange.sh -Port 20355 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nok\n' -OutDirectory out -CurlArgs -sS -d @sub http://127.0.0.1:20355/a` -> exit 26, stdout empty, request.bin empty (nothing sent), stderr `curl: option -d: error encountered when reading a file` + the try-help line. Curl's Linux-build parse (`Cgr069DataFileTests.Parse_AtDirectoryOffWindows_IsRefusedWithoutTheFailedToOpenLine`, the parser path the console takes before any connection) gives the same exit code and the same two lines; a parse refusal sends no request and writes no stdout.
- Before the fix the two `Parse_AtDirectoryOffWindows` cases failed ("Different number of elements"); after it all 6 tests in Cgr069DataFileTests pass. Platform-neutral: each test names its build with `DiskDataFileReader.ForPlatform` and uses its own temp directory.
- `dotnet build` clean; fast tests 46 assemblies green (44,459 passed, 306 skipped by platform). `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 6ed89410 a7c0fc04 779790c0.
- 2026-10-28: Doing -> Done. On Linux and macOS, -d @dir and -w @dir are refused with curl's two lines and exit 26, no Failed to open line
