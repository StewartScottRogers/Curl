---
id: BL-3900
title: Windows: -o, -T and config-file file names take the mingw curl's ANSI bytes (CGR-114)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/WindowsOutputFileNameSanitizer.cs, Curl.Console.UnitTests/Cgr114FileNameTests.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitTests/Cgr114FileNameTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3900 — Windows: -o, -T and config-file file names take the mingw curl's ANSI bytes (CGR-114)

## Goal

On Windows, file names given to `-o`, `-T` and a config file's `output` reach the file system as the curl 8.22.0 mingw (non-UNICODE) build sees them, so the CGR-114 Windows repros match real curl.

## Context

CGR-114 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (filed from BL-3643). The reference build receives its arguments in the system ANSI code page with best fit (ADR-0018, ADR-0067) and reads a config file's bytes as they are. Real curl on a Windows-1252 system:
- `curl -s -o 日本語.bin http://127.0.0.1:<port>/bin` writes `___.bin` (best fit gives `???`, and the Windows output-name sanitizer turns `?` into `_`).
- `curl -s -T 日本.txt http://127.0.0.1:<port>/echo` prints `curl: cannot open '??.txt'` and exits 26.
- A UTF-8 config file holding `output = "café.bin"` with `curl -s -K c.cfg http://127.0.0.1:<port>/bin` creates `cafÃ©.bin` (the UTF-8 bytes read in the ANSI code page). ADR-0446 keeps UTF-8 header text from a config file; this concerns file names only, so record in an ADR where the line is drawn.
Curl today writes `日本語.bin`, uploads `日本.txt`, and creates `café.bin` (as a UNICODE build, such as System32 curl 8.21, does). Find where `-o`, `-T` and `output` values reach the file system in `Curl.Console` (start at `CurlCommandRunner.cs` and `WindowsOutputFileNameSanitizer.cs`) and narrow or widen `touches` once that is known. Linux and macOS are unchanged (arguments are bytes there). Measure real curl with `Record-CurlExchange.ps1` before pinning text.

## Acceptance criteria

- [x] On Windows, the three repros above give real curl's exit code, stderr text and file written; the commands and both results are recorded in Notes.
- [x] Focused unit tests in `Curl.Console.UnitTests` pin each case with the ANSI code page injected, so they run on every platform.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records which file names take ANSI bytes and why.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured 2026-10-26 with Record-CurlExchange.ps1 on this Windows-1252 host (ANSI code page 1252), ucrt64 curl 8.22.0, each run in an empty directory: `curl -s -o 日本語.bin http://127.0.0.1:18390/bin` exit 0, empty stderr, writes `___.bin`; `curl -s -T 日本.txt http://127.0.0.1:18391/echo` exit 26, stderr `curl: cannot open '??.txt'` then the try-help line; UTF-8 `c.cfg` holding `output = "café.bin"` with `curl -s -K c.cfg http://127.0.0.1:18392/bin` exit 0, empty stderr, creates `cafÃ©.bin`.
- Curl before the change: the first two already matched (BL-3892 reads arguments through the ANSI code page, BL-3544 echoes the name in it); the config case created `café.bin`. After: Curl gives exit 0, empty stderr and `cafÃ©.bin`, as real curl.
- Fix: `CommandLineOptions.AddOutputFile` stores `AsWireText(outputFile)`, the re-spelling header, user-agent, referer and -w text from a UTF-8 config file already get. Touches widened to `Curl.Cli.UnitLibrary/CommandLineOptions.cs` and `Curl.Cli.UnitTests/Cgr114FileNameTests.cs` (the `-o` value is stored there, where the config-file state is known; no Doing task on origin/work/dark-factory names either file; BL-3641 holds CommandLineOptionTable.cs, which was avoided). CurlCommandRunner.cs and WindowsOutputFileNameSanitizer.cs needed no change.
- Decision recorded in ADR-0846: only a config file's `output` is re-spelled among config-file names, being the one measured.
- Tests: Curl.Console.UnitTests/Cgr114FileNameTests.cs (arguments through an injected Windows-1252; the config case reads the code page as the parser does, Windows-1252 where the host names none) and Curl.Cli.UnitTests/Cgr114FileNameTests.cs. Platform-neutral: no paths or OS errors. Build clean; fast tests 44262 passed, 0 failed; Measure-CodeQuality -Library Curl.Cli.UnitLibrary: 0 failing members.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: d02b924d bd4de3db b3264596 779790c0.
- 2026-10-26: Doing -> Done. On Windows a UTF-8 config file's output name is its bytes read in the ANSI code page (cafÃ©.bin), and -o/-T names match the mingw curl's best-fit names
