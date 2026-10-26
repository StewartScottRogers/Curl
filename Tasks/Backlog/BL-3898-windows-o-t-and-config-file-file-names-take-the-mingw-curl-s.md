---
id: BL-3898
title: Windows: -o, -T and config-file file names take the mingw curl's ANSI bytes (CGR-114)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/WindowsOutputFileNameSanitizer.cs, Curl.Console.UnitTests/Cgr114FileNameTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3898 — Windows: -o, -T and config-file file names take the mingw curl's ANSI bytes (CGR-114)

## Goal

On Windows, file names given to `-o`, `-T` and a config file's `output` reach the file system as the curl 8.22.0 mingw (non-UNICODE) build sees them, so the CGR-114 Windows repros match real curl.

## Context

CGR-114 in `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (filed from BL-3643). The reference build receives its arguments in the system ANSI code page with best fit (ADR-0018, ADR-0067) and reads a config file's bytes as they are. Real curl on a Windows-1252 system:
- `curl -s -o 日本語.bin http://127.0.0.1:<port>/bin` writes `___.bin` (best fit gives `???`, and the Windows output-name sanitizer turns `?` into `_`).
- `curl -s -T 日本.txt http://127.0.0.1:<port>/echo` prints `curl: cannot open '??.txt'` and exits 26.
- A UTF-8 config file holding `output = "café.bin"` with `curl -s -K c.cfg http://127.0.0.1:<port>/bin` creates `cafÃ©.bin` (the UTF-8 bytes read in the ANSI code page). ADR-0446 keeps UTF-8 header text from a config file; this concerns file names only, so record in an ADR where the line is drawn.
Curl today writes `日本語.bin`, uploads `日本.txt`, and creates `café.bin` (as a UNICODE build, such as System32 curl 8.21, does). Find where `-o`, `-T` and `output` values reach the file system in `Curl.Console` (start at `CurlCommandRunner.cs` and `WindowsOutputFileNameSanitizer.cs`) and narrow or widen `touches` once that is known. Linux and macOS are unchanged (arguments are bytes there). Measure real curl with `Record-CurlExchange.ps1` before pinning text.

## Acceptance criteria

- [ ] On Windows, the three repros above give real curl's exit code, stderr text and file written; the commands and both results are recorded in Notes.
- [ ] Focused unit tests in `Curl.Console.UnitTests` pin each case with the ANSI code page injected, so they run on every platform.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records which file names take ANSI bytes and why.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
