---
id: BL-5933
title: Measure whether Windows curl replaces the -c, --hsts and --alt-svc files by rename and match it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/FileSystem, Curl.Core.UnitTests/FileSystem, Curl.Protocol.Abstractions.UnitLibrary/FileWriteMode.cs, Documentation/Planning/Decisions/ADR-1368-state-files-are-replaced-by-rename-off-windows.md]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5933 — Measure whether Windows curl replaces the -c, --hsts and --alt-svc files by rename and match it

## Goal

On Windows, saving the `-c` jar, the `--hsts` file and the `--alt-svc` file leaves each file as the Windows reference curl leaves it.

## Context

BL-5888 made the three files be replaced by rename off Windows and left Windows writing in place, unmeasured (ADR-1368). Measure the reference curl on Windows over an existing file, a read-only file, a hard-linked file and a file another process holds open (compare the NTFS file ID, attributes and content before and after), then match it in `Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs` or record that it writes in place.

## Acceptance criteria

- [x] An ADR records what the Windows reference curl does to each of the three files in the four cases.
- [x] Tests in `Curl.Core.UnitTests` marked `[OSCondition(OperatingSystems.Windows)]` pin Curl's matching behaviour.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-11-25: Git for Windows curl 8.21.0 and 8.22.0 and System32 curl 8.23.0 replace all three files by temporary file and rename, retrying the rename for 1 s (table in ADR-1376). A scratch script ran each case over a loopback HTTP server; Curl.exe then matched curl 8.22.0 in all 33 cases (11 per file: file ID, attributes, content, leftovers, exit code, stderr).
- A `file://` transfer saved none of the three files in curl 8.21.0. That is when they are saved, not how, so it was left out of this task.
- touches widened (no other task in Doing named either): `FileWriteMode.cs` said Windows truncates, and ADR-1368 gained a superseded note. The FileWriteMode.cs change is a doc comment only, so no quality run for Abstractions.
- Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary found one member: lines 180-182 of ReplacementFileStream.TryRename (the IOException catch) were uncovered. Added Dispose_TemporaryFileGone_ThrowsAndKeepsTheOld, which reaches it; Curl.Core.UnitTests pass. The 45-minute quality run was not repeated.
- Full solution build clean; fast tests 58,049 passed, 0 failed (before that one added test).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: ed061db0 15eca511 3d836d5e.
- 2026-11-25: Doing -> Done. On Windows the -c, --hsts and --alt-svc files are replaced by rename with a one-second retry, as measured of curl 8.21.0 to 8.23.0 (ADR-1376)
