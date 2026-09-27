---
id: BL-246
title: Use a file's modification time for a -z value that is not a date
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-138]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: FR-009
created: 2026-09-26
completed: 2026-09-26
---
# BL-246 — Use a file's modification time for a -z value that is not a date

## Goal

`curl -z <file>` uses the named file's modification time as the `-z` date, as curl 8.21.0
does, when the value is not a date.

## Context

- BL-138 added `-z`/`--time-cond` (`CommandLineOptionTable.SetTimeCondition`,
  `CurlDateParser`). A value that is not a date currently always gets
  `CommandLineWarning.TimeConditionIsNotADate` and no condition.
- Upstream (tool `getparameter`, `C_TIME_COND`, curl 8.21.0): when `curl_getdate` fails, the
  value is tried as a file name through `getfiletime`; its modification time becomes the
  condition's date, keeping the direction from the `-`/`+`/`=` prefix. Only when that fails too
  does curl print the two warning lines and drop the condition.
- Measured on curl 8.21.0 (Windows, 2026-09-26): `curl -v -z CLAUDE.md file:///Z:/.../global.json`
  printed `* The requested document is not new enough` (CLAUDE.md newer than global.json).
- On Windows `getfiletime` prints `Warning: Failed to get filetime: CreateFile failed:
  GetLastError 0x00000003` before the two illegal-date lines when `CreateFile` fails with
  anything but file-not-found: measured for `-z ""` and `-z -` (curl 8.21.0, 2026-09-26);
  `-z notadate` (file not found) prints only the two lines.
- The parser takes its file-system seams as injected parameters (`pathExists`,
  `IDataFileReader`); the file's modification time needs one too, so tests never touch the disk.

## Acceptance criteria

- [x] A `-z` value that is not a date but names a file (through an injected seam) gives a
      `TimeCondition` at that file's modification time, with the prefix's direction; a test in
      `Curl.Cli.UnitTests` shows it for both `-z <file>` and `-z -<file>`.
- [x] A value that names no file still gives the two warning lines and no condition.
- [x] The `Failed to get filetime` line for a lookup failing other than as file-not-found is
      measured on curl 8.21.0 for the cases the seam can report, recorded in `Notes`, and
      asserted byte for byte, or a follow-up task is filed for it.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and
      `Measure-CodeQuality.ps1` reports no failing member in `Curl.Cli.UnitLibrary`.

## Notes

- Filed from BL-138, which chose to defer the fallback: the applier signature carries only
  `pathExists` and `IDataFileReader`, and adding a modification-time seam is its own change.
- Plan (run directly rather than through the full /feature subagent chain: one option, one
  library and its tests). The seam is a new `IDataFileReader.TryReadModificationTime(path, out
  time, out failureReason)`, not a new applier parameter, so only `SetTimeCondition` changes.
  Decision and alternatives in ADR-0034 (Decided by Claude under Stewart's delegation).
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0034 and its index row; no task
  in Doing names it.
- Measured on curl 8.21.0 (mingw, Schannel, Windows), 2026-09-26, `curl -z <v> -o NUL
  file:///Z:/repos/Curl.lanes/lane-4/global.json`, every case exit 0 with the transfer:
  `notadate` two illegal-date lines only; `""`, `-`, `nodir/x`, `global.json/x` first print
  `Warning: Failed to get filetime: CreateFile failed: GetLastError 0x00000003`; a directory and
  `C:/Windows/System32/config/SAM` `0x00000005`; `C:/pagefile.sys` `0x00000020`; `x*y`
  `0x0000007b`; `con` `0x00000057`; `nul` `GetFileTime failed: GetLastError 0x00000057`.
  `-s` (with or without `-S`) prints nothing. `-z CLAUDE.md` printed `* The requested document
  is not new enough`; `-z -CLAUDE.md` transferred.
- Asserted byte for byte: the filetime line for `-z ""`/`-z -` in
  `CommandLineTimeConditionOptionTests`, and the 3/5/0x20/0x7b reasons from the matching .NET
  exceptions in `DiskDataFileReaderTests`. The time is truncated to the whole second (curl's
  `time_t`).
- Left for BL-286: the non-Windows `stat`/`strerror` lines (unmeasured, so off Windows every
  failure reads as file not found) and the `con`/`nul` device cases.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` 100% line, 100% branch,
  0 failing of 572 members (the fallback was split into `TryReadTimeConditionDate` and
  `DisableTimeCondition` to keep `SetTimeCondition` under complexity 10).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -z <file> uses the file's modification time when the value is not a date, with curl's Windows filetime warning on a failed lookup
