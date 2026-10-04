---
id: BL-1425
title: Stamp an -R time past year 9999 and cap it above 30827-12-31 on Windows with curl's 'Capping set filetime to max' warning
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1421, BL-1422]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: FR-011
created: 2026-10-03
completed:
---
# BL-1425 — Stamp an -R time past year 9999 and cap it above 30827-12-31 on Windows with curl's 'Capping set filetime to max' warning

## Goal

`-R`/`--remote-time` stamps the `-o` file with a source time past year 9999; on Windows a time above 30827-12-31T23:59:59Z is capped to it with `Warning: Capping set filetime to max to avoid overflow`, exactly as curl 8.21.0 (Schannel) does, and off Windows the time is passed on uncapped as curl's `utimes` branch does.

## Context

- ADR-0410, decision 4. Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel), `--no-progress-meter -R -o <file>` against `Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT`: stderr `Warning: Capping set filetime to max to avoid overflow`, file stamped 30827-12-31T23:59:59Z, exit 0.
- curl 8.21.0 `src/tool_filetime.c` lines 94-98: on Windows a Unix time above `910670515199` is set to `910670515199` with that warning unless `-s`; line 128 on (POSIX) hands the time to `utimes` unchanged.
- `Curl.Console/CurlCommandRunner.cs` `StampOutputFileTimeAsync` already has BL-1392's minimum cap (`WindowsMinimumFileTimeUtc`, `FileTimeCappedToMinimumWarning`, `runsOnWindows`); mirror it. `Curl.Core.UnitLibrary/FileSystem/IFileTimeSetter.cs` takes a `DateTimeOffset` and `PhysicalFileSystem` uses `File.SetLastWriteTimeUtc(DateTime)`, which cannot hold 30827: make it take Unix seconds and set times past 9999 itself (`SetFileTime` on Windows, `utimensat` off Windows, via `LibraryImport`). `RemoteTimeFailureWarning.For` prints the Unix seconds it is given.
- Depends on BL-1421 (`SourceLastWriteUnixSeconds`) and BL-1422 (HTTP produces such a time, for the end-to-end test).

## Acceptance criteria

- [ ] With the runner's `runsOnWindows: true`, a result whose `SourceLastWriteUnixSeconds` is 1200110860800 makes the setter receive 910670515199 and stderr carry exactly `Warning: Capping set filetime to max to avoid overflow`, before any `Failed to set filetime` line; exit 0 (test).
- [ ] With `-s` the cap still applies and no warning is written (test).
- [ ] With `runsOnWindows: false` the setter receives 1200110860800 unchanged and no capping warning is written (test).
- [ ] A time of exactly 910670515199 is not capped and prints nothing (test); BL-1392's minimum-cap tests still pass.
- [ ] `PhysicalFileSystem` sets a time past 9999 on a real temp file: on Windows 910670515199 reads back as 30827-12-31T23:59:59Z through the Win32 file time (Windows-only test); off Windows a call succeeds or fails with the failure line, never throws (test).
- [ ] An end-to-end console test with a fake HTTP reply `Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT`, `--no-progress-meter -R -o <file>` on Windows, pins the measured stderr, stamp and exit 0.
- [ ] `dotnet build` is clean, fast tests pass, `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Console` reports 0 failing members; no option changes, so `--ai-help` is unaffected.

## Notes

## Log

- 2026-10-03: Created.
