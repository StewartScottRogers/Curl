---
id: BL-3856
title: CGR-016 part 6: -w @file writes the file's bytes that are invalid UTF-8 unchanged
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr016WriteOutFileTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3856 — CGR-016 part 6: -w @file writes the file's bytes that are invalid UTF-8 unchanged

## Goal

`curl -s -o /dev/null -w @fmt URL`, where `fmt` holds a byte that is not valid UTF-8 (FE), writes that byte unchanged, as curl 8.18.0 does on Linux (and the Schannel curl on Windows, once measured).

## Context

Found by BL-3852 (CGR-016 part 4, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). `CommandLineOptionTable.SetWriteOut` (`Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`) reads the `@file` bytes and decodes them with `Encoding.UTF8.GetString`, so an invalid byte becomes U+FFFD and is written as EF BF BD. Decoding with `InvalidUtf8ByteEscape.Decode` instead hands the renderer the lone surrogate U+DC00+b, which `WriteOutTemplateRenderer` already writes as the byte b (BL-3852). Measure real curl first with `Record-CurlExchange.ps1` (WSL for Linux, and the Windows curl), since this changes the Windows build too; no Python.

## Acceptance criteria

- [ ] `-w @file` with a file holding FE writes the same stdout bytes as curl 8.18.0 on Linux; the measured command and both results are recorded in Notes, and the Windows curl's answer is measured and matched.
- [ ] A focused unit test in `Curl.Cli.UnitTests/Cgr016WriteOutFileTests.cs` fails before the change and passes after it on every platform.
- [ ] Curl.Cli.UnitLibrary stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
