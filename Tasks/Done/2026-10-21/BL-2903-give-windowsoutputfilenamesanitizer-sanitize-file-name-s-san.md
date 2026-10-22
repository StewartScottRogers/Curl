---
id: BL-2903
title: Give WindowsOutputFileNameSanitizer sanitize_file_name's SANITIZE_ALLOW_PATH and SANITIZE_ALLOW_RESERVED modes for tool1604
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/Globbing/WindowsOutputFileNameSanitizer.cs, Curl.Core.UnitTests/Globbing/WindowsOutputFileNameSanitizerTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2903 — Give WindowsOutputFileNameSanitizer sanitize_file_name's SANITIZE_ALLOW_PATH and SANITIZE_ALLOW_RESERVED modes for tool1604

## Goal

`WindowsOutputFileNameSanitizer` has curl's `sanitize_file_name` flags `SANITIZE_ALLOW_PATH` and `SANITIZE_ALLOW_RESERVED`, so the port of `tests/tunit/tool1604.c` (BL-2845) passes on every OS.

## Context

Today it has one mode, `Sanitize(string)`. tool1604.c's table covers banned characters, drive letters, `\\?\` paths, reserved device names (`com1`, `CoM1`, `con.air`), trailing dots and spaces, and a NULL input (`SANITIZE_ERR_BAD_ARGUMENT`). Source: https://github.com/curl/curl/blob/curl-8_22_0/src/tool_doswin.c and tests/tunit/tool1604.c. Pure string work, so it runs on every OS.

## Acceptance criteria

- [x] The sanitizer takes the two flags and gives every non-MSDOS row of tool1604.c (output and result).
- [x] `Curl.Conformance.UnitProgram.UnitLibrary` can call the counterpart (public, or `InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary"` in its library's csproj), so BL-2845 can port the program without touching this library.
- [x] A focused unit test in the library's own `.UnitTests` project pins every row of the C program's table (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Ported curl 8.22.0's `sanitize_file_name` (src/tool_doswin.c) as `WindowsOutputFileNameSanitizer.SanitizeFileName(string?, SanitizeFlags, out string?)`, returning `SanitizeResult` (`Ok` = 0, `BadArgument` = 2, curl's `SANITIZE_ERR_*` values). `Sanitize(string)` now calls it with `AllowPath | AllowReserved`; its behaviour is unchanged.
- Choices (sensible defaults): the class is now `public`, so `Curl.Conformance.UnitProgram.UnitLibrary` can call it without a csproj change (the csproj is not in `touches`); the flag and result enums are nested in the class to keep one public type per file; the new overload is named `SanitizeFileName` after curl's function, because a second `Sanitize` overload made the unqualified crefs in `Curl.Console/WindowsOutputFileNameSanitizer.cs` ambiguous (CS0419), and that file is outside `touches`. The MSDOS-only steps (`msdosify`, the device `stat`) are not ported: no Windows build has them.
- `WindowsOutputFileNameSanitizerTests` pins every non-MSDOS tool1604.c row (35 rows and the NULL row) plus the branches the table leaves out (`CLOCK$`, `PRN`, `AUX`, `NUL`, `LPT9`, `COM` alone, `com0`, paths allowed).
- Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary: 100% line, 100% branch, 0 failing members, worst CRAP 10. The first run had `IsBanned` at 12 and `RenameReservedDeviceNameAt` at 14, so both were split into smaller methods.

## Log

- 2026-10-21: Created (split from BL-2845, which waits on it).
- 2026-10-21: Backlog -> Doing. Criteria at claim: e68820c6 3d228e1f d3a9ab15 099d1d27.
- 2026-10-21: Doing -> Done. WindowsOutputFileNameSanitizer.SanitizeFileName takes SANITIZE_ALLOW_PATH and SANITIZE_ALLOW_RESERVED and gives every non-MSDOS tool1604.c row, public for BL-2845
