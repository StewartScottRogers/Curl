---
id: BL-1392
title: Cap an -R time before 14 September 1752 to that date on Windows with curl's 'Capping set filetime to minimum' warning
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1391]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: FR-011
created: 2026-10-03
completed:
---
# BL-1392 — Cap an -R time before 14 September 1752 to that date on Windows with curl's 'Capping set filetime to minimum' warning

## Goal

On Windows, `-R`/`--remote-time` stamps an output file whose source time is earlier than 1752-09-14T00:00:00Z (Unix -6857222400) with exactly that time and, unless `-s`, first writes curl 8.21.0's `Warning: Capping set filetime to minimum to avoid overflow`; off Windows the time is set as given, with no warning.

## Context

- Today `Curl.Console/CurlCommandRunner.cs` `StampOutputFileTimeAsync` (around line 4150) passes the source time straight to `IFileTimeSetter.TrySetLastWriteTimeUtc` (`Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs` line 130), with no lower bound and no warning; `git grep "Capping set filetime"` finds nothing.
- curl 8.21.0 (tag `curl-8_21_0`), `src/tool_filetime.c` lines 86-105, the `_WIN32` branch of `setfiletime`: `if(filetime > 910670515199) { filetime = 910670515199; warnf("Capping set filetime to max to avoid overflow"); } else if(filetime < -6857222400) { filetime = -6857222400; warnf("Capping set filetime to minimum to avoid overflow"); }` before `SetFileTime`. The POSIX `utimes` branch (lines 128 on) has no cap.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1`, `--no-progress-meter -R -o <file>`:
  - `Last-Modified: Mon, 01 Jan 1700 00:00:00 GMT`: stderr `Warning: Capping set filetime to minimum to avoid overflow`; the file's time is 1752-09-14T00:00:00Z; exit 0.
  - `Last-Modified: Thu, 14 Sep 1752 00:00:00 GMT`: no warning; time 1752-09-14T00:00:00Z.
  - `Wed, 13 Sep 1752 23:59:59 GMT` with `-s`: no warning (muted), time capped to 1752-09-14T00:00:00Z.
  - `Mon, 01 Jan 40000 00:00:00 GMT`: `Warning: Capping set filetime to max to avoid overflow`, time 30827-12-31T23:59:59Z. A `DateTimeOffset` cannot hold a year past 9999, so that case is not this task's (see Notes).

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` marked `[OSCondition(OperatingSystems.Windows)]` runs an `-R -o` transfer whose source time is 1700-01-01T00:00:00Z through a recording fake `IFileTimeSetter` and asserts the time set is 1752-09-14T00:00:00Z and the stderr line `Warning: Capping set filetime to minimum to avoid overflow`, written before any `Failed to set filetime` warning would be.
- [ ] Windows-only tests pin 1752-09-14T00:00:00Z exactly (no warning, unchanged) and one second earlier under `-s` (capped, no warning).
- [ ] A test marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` pins that off Windows the 1700 time is passed unchanged and no warning is written.
- [ ] `dotnet build Curl.Console.UnitTests -warnaserror` is clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member in the code this task changed.

## Notes

- The maximum cap needs source times past year 9999, which `TransferResult`'s `DateTimeOffset` cannot carry; carrying them is a change to `Curl.Protocol.Abstractions.UnitLibrary` and the protocol parsers, left for its own task.
- Depends on BL-1391 only because both change `Curl.Console`. No option changes, so `--ai-help` is unaffected.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
