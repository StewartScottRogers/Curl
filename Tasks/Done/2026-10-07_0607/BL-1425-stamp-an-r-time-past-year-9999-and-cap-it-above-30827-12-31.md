---
id: BL-1425
title: Stamp an -R time past year 9999 and cap it above 30827-12-31 on Windows with curl's 'Capping set filetime to max' warning
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1427, BL-1428]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: FR-011
created: 2026-10-03
completed: 2026-10-03
---
# BL-1425 — Stamp an -R time past year 9999 and cap it above 30827-12-31 on Windows with curl's 'Capping set filetime to max' warning

## Goal

`-R`/`--remote-time` stamps the `-o` file with a source time past year 9999; on Windows a time above 30827-12-31T23:59:59Z is capped to it with `Warning: Capping set filetime to max to avoid overflow`, exactly as curl 8.21.0 (Schannel) does, and off Windows the time is passed on uncapped as curl's `utimes` branch does.

## Context

- ADR-0410, decision 4. Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel), `--no-progress-meter -R -o <file>` against `Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT`: stderr `Warning: Capping set filetime to max to avoid overflow`, file stamped 30827-12-31T23:59:59Z, exit 0.
- curl 8.21.0 `src/tool_filetime.c` lines 94-98: on Windows a Unix time above `910670515199` is set to `910670515199` with that warning unless `-s`; line 128 on (POSIX) hands the time to `utimes` unchanged.
- `Curl.Console/CurlCommandRunner.cs` `StampOutputFileTimeAsync` already has BL-1392's minimum cap (`WindowsMinimumFileTimeUtc`, `FileTimeCappedToMinimumWarning`, `runsOnWindows`); mirror it. `Curl.Core.UnitLibrary/FileSystem/IFileTimeSetter.cs` takes a `DateTimeOffset` and `PhysicalFileSystem` uses `File.SetLastWriteTimeUtc(DateTime)`, which cannot hold 30827: make it take Unix seconds and set times past 9999 itself (`SetFileTime` on Windows, `utimensat` off Windows, via `LibraryImport`). `RemoteTimeFailureWarning.For` prints the Unix seconds it is given.
- Depends on BL-1427 (`SourceLastWriteUnixSeconds`) and BL-1428 (HTTP produces such a time, for the end-to-end test).

## Acceptance criteria

- [x] With the runner's `runsOnWindows: true`, a result whose `SourceLastWriteUnixSeconds` is 1200110860800 makes the setter receive 910670515199 and stderr carry exactly `Warning: Capping set filetime to max to avoid overflow`, before any `Failed to set filetime` line; exit 0 (test).
- [x] With `-s` the cap still applies and no warning is written (test).
- [x] With `runsOnWindows: false` the setter receives 1200110860800 unchanged and no capping warning is written (test).
- [x] A time of exactly 910670515199 is not capped and prints nothing (test); BL-1392's minimum-cap tests still pass.
- [x] `PhysicalFileSystem` sets a time past 9999 on a real temp file: on Windows 910670515199 reads back as 30827-12-31T23:59:59Z through the Win32 file time (Windows-only test); off Windows a call succeeds or fails with the failure line, never throws (test).
- [x] An end-to-end console test with a fake HTTP reply `Last-Modified: Mon, 01 Jan 40000 00:00:00 GMT`, `--no-progress-meter -R -o <file>` on Windows, pins the measured stderr, stamp and exit 0.
- [x] `dotnet build` is clean, fast tests pass, `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Console` reports 0 failing members; no option changes, so `--ai-help` is unaffected.

## Notes

- `IFileTimeSetter.TrySetLastWriteTimeUtc` became `TrySetLastWriteUnixSeconds(string, long, out int)`; the runner passes `TransferResult.SourceLastWriteUnixSeconds` and `RemoteTimeFailureWarning.For` takes Unix seconds.
- The runner caps on Windows with one switch (`CapToWindowsFileTime`): below -6857222400 to the minimum, above 910670515199 to the maximum, each with curl's warning muted by `-s`.
- `PhysicalFileSystem` keeps `File.SetLastWriteTimeUtc` for times `DateTime` holds and hands the rest to the new `NativeFileTimeSetter` (Core): `SetFileTime` on Windows, `utimes` off Windows. Choice: `utimes` rather than the `utimensat` the Context named, because it is exactly curl 8.21.0's POSIX call (`tool_filetime.c` line 128 on) and needs no platform-specific `AT_FDCWD` value. Like the other native adapters it is `[ExcludeFromCodeCoverage]` under ADR-0083; the Windows path is exercised by `PhysicalFileSystemTests` (30827 round-trips through raw `GetFileTime`).
- Core and Core.UnitTests now set `AllowUnsafeBlocks` for the `LibraryImport` stubs.
- Moved the stray `-D` summary in `CurlCommandRunner` back onto `StandardOutputHeaderFile`.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Console`: 0 failing members. Fast tests: all green (Core 1396, Console 2594 passed).

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. -R stamps times past 9999; Windows caps above 30827-12-31 with curl's 'Capping set filetime to max' warning
