---
id: BL-3852
title: CGR-016 part 4: Linux and macOS write -w format bytes that are invalid UTF-8 unchanged
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3849]
touches: [Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs, Curl.Output.UnitTests/Cgr016WriteOutTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3852 — CGR-016 part 4: Linux and macOS write -w format bytes that are invalid UTF-8 unchanged

## Goal

On Linux and macOS, `curl -s -o /dev/null -w $'\xfe' URL` writes the single byte 0xFE to stdout, as curl 8.18.0 does (today EF BF BD).

## Context

Split from BL-3545 (third-party gap report `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, CGR-016). On Linux, .NET hands `Main` an argv already decoded as UTF-8 with every invalid byte replaced by U+FFFD, so every option then re-encodes EF BF BD where curl 8.18.0 (OpenSSL, Linux) uses the raw byte (curl never re-encodes argv). Measure real curl in WSL with `Record-CurlExchange.ps1` before pinning output; no Python.

Needs BL-3849's lossless argument decoding: each invalid byte b arrives as the lone surrogate U+DC00+b. `Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs` renders the template's literal text; write such a surrogate as its byte rather than U+FFFD's UTF-8. Check `-w @file` and `%output{}` file names too.

## Acceptance criteria

- [ ] `-o /dev/null -w $'\xfe'` writes the same stdout bytes as curl 8.18.0 on Linux; the measured command and both results are recorded in Notes.
- [ ] Valid UTF-8 `-w` text, and every `-w` text on Windows, is written as before.
- [ ] A focused unit test in `Curl.Output.UnitTests/Cgr016WriteOutTests.cs` fails before the change and passes after it on every platform.
- [ ] Curl.Output.UnitLibrary stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 6fd990b3 fa400498 03933128 f9e9638a 779790c0.
