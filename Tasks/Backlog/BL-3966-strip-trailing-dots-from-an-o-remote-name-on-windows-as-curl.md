---
id: BL-3966
title: Strip trailing dots from an -O remote name on Windows as curl's sanitizer does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/WindowsOutputFileNameSanitizer.cs, Curl.Console.UnitTests/WindowsOutputFileNameSanitizerTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3966 — Strip trailing dots from an -O remote name on Windows as curl's sanitizer does

## Goal

On Windows, `curl -sS -O --path-as-is --output-dir <dir> "http://127.0.0.1:<port>/a/..." -w "[%{filename_effective}]"` gives the same exit code, stdout and stderr as curl 8.21.0 Schannel.

## Context

Found by BL-3592 (CGR-063), measured 2026-10-28 with Record-CurlExchange.ps1 against curl 8.21.0 Schannel on Windows: for the path `/a/...` curl prints `[<dir>/]`, exits 23 with `curl: (23) client returned ERROR on write of 2 bytes` and writes no file, because curl's Windows sanitizer (`sanitize_file_name`, tool_doswin.c) strips trailing dots and spaces from the name. Curl prints `[<dir>/...]` with the same exit 23. Start at `Curl.Console/WindowsOutputFileNameSanitizer.cs` `SanitizeRemoteName`. Measure other trailing-dot and trailing-space names (`a.`, `a. `, `...`) before pinning them.

## Acceptance criteria

- [ ] The repro in Goal, run through real curl and Curl on Windows, gives the same exit code, stdout bytes, stderr text and files written; the commands and results are in Notes.
- [ ] A unit test marked `[OSCondition(OperatingSystems.Windows)]` in `Curl.Console.UnitTests/WindowsOutputFileNameSanitizerTests.cs` pins the stripped name.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
