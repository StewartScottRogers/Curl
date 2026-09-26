---
id: BL-246
title: Use a file's modification time for a -z value that is not a date
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-138]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: FR-009
created: 2026-09-26
completed:
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

- [ ] A `-z` value that is not a date but names a file (through an injected seam) gives a
      `TimeCondition` at that file's modification time, with the prefix's direction; a test in
      `Curl.Cli.UnitTests` shows it for both `-z <file>` and `-z -<file>`.
- [ ] A value that names no file still gives the two warning lines and no condition.
- [ ] The `Failed to get filetime` line for a lookup failing other than as file-not-found is
      measured on curl 8.21.0 for the cases the seam can report, recorded in `Notes`, and
      asserted byte for byte, or a follow-up task is filed for it.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and
      `Measure-CodeQuality.ps1` reports no failing member in `Curl.Cli.UnitLibrary`.

## Notes

- Filed from BL-138, which chose to defer the fallback: the applier signature carries only
  `pathExists` and `IDataFileReader`, and adding a modification-time seam is its own change.

## Log

- 2026-09-26: Created.
