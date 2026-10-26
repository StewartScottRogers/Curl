---
id: BL-3850
title: CGR-016 part 2: Linux and macOS send -H header values and -b cookie values with invalid UTF-8 argument bytes unchanged
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3849]
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineCookie.cs, Curl.Cli.UnitTests/Cgr016HeaderCookieTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3850 — CGR-016 part 2: Linux and macOS send -H header values and -b cookie values with invalid UTF-8 argument bytes unchanged

## Goal

On Linux and macOS, `-H $'X-A: \xff'` sends the header line `X-A: ` followed by the byte 0xFF, and `-b $'a=\xff'` sends `Cookie: a=` followed by 0xFF, as curl 8.18.0 does (today EF BF BD and `?`).

## Context

Split from BL-3545 (third-party gap report `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, CGR-016). On Linux, .NET hands `Main` an argv already decoded as UTF-8 with every invalid byte replaced by U+FFFD, so every option then re-encodes EF BF BD where curl 8.18.0 (OpenSSL, Linux) uses the raw byte (curl never re-encodes argv). Measure real curl in WSL with `Record-CurlExchange.ps1` before pinning output; no Python.

Needs BL-3849's lossless argument decoding (`InvalidUtf8ByteEscape`). The request side writes header and cookie strings one character per byte (Latin-1): see `AsRawWireText` in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` (near line 1014), which re-spells config-file values that way (CGR-020); extend it so a command-line value holding escaped bytes is re-spelled through the argument wire encoding too, and do the same for `-b` cookie strings (`CommandLineCookie.cs`). Check `--proxy-header` and `-X` go the same way.

## Acceptance criteria

- [ ] `-H $'X-A: \xff'` and `-b $'a=\xff'` send the same request bytes as curl 8.18.0 on Linux; the measured commands and both results are recorded in Notes.
- [ ] Valid UTF-8 header and cookie values, and every value on Windows, go out as before.
- [ ] A focused unit test in `Curl.Cli.UnitTests/Cgr016HeaderCookieTests.cs` fails before the change and passes after it on every platform.
- [ ] Curl.Cli.UnitLibrary stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
