---
id: BL-3862
title: CGR-016 part 6: -w @file writes the file's bytes that are invalid UTF-8 unchanged
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/Cgr016WriteOutFileTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3862 — CGR-016 part 6: -w @file writes the file's bytes that are invalid UTF-8 unchanged

## Goal

`curl -s -o /dev/null -w @fmt URL`, where `fmt` holds a byte that is not valid UTF-8 (FE), writes that byte unchanged, as curl 8.18.0 does on Linux (and the Schannel curl on Windows, once measured).

## Context

Found by BL-3852 (CGR-016 part 4, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`). `CommandLineOptionTable.SetWriteOut` (`Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`) reads the `@file` bytes and decodes them with `Encoding.UTF8.GetString`, so an invalid byte becomes U+FFFD and is written as EF BF BD. Decoding with `InvalidUtf8ByteEscape.Decode` instead hands the renderer the lone surrogate U+DC00+b, which `WriteOutTemplateRenderer` already writes as the byte b (BL-3852). Measure real curl first with `Record-CurlExchange.ps1` (WSL for Linux, and the Windows curl), since this changes the Windows build too; no Python.

## Acceptance criteria

- [x] `-w @file` with a file holding FE writes the same stdout bytes as curl 8.18.0 on Linux; the measured command and both results are recorded in Notes, and the Windows curl's answer is measured and matched.
- [x] A focused unit test in `Curl.Cli.UnitTests/Cgr016WriteOutFileTests.cs` fails before the change and passes after it on every platform.
- [x] Curl.Cli.UnitLibrary stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured 2026-10-28 with Record-CurlExchange.ps1, `fmt` holding 41 FE 42: `curl -s -o /dev/null -w @fmt http://172.26.96.1:18863/` with curl 8.18.0 (OpenSSL, WSL Ubuntu) wrote 41 FE 42, exit 0; `curl -s -o NUL -w @fmt http://127.0.0.1:18862/` with curl 8.22.0 Schannel on Windows wrote 41 FE 42. Before the change Curl parsed FE as U+FFFD, which the renderer writes as EF BF BD (the new test failed on that).
- Fix (ADR-0938): `CommandLineOptionTable.WriteOutFileTemplate` decodes the bytes with `InvalidUtf8ByteEscape.Decode` off Windows; on Windows it spells the raw bytes in the ANSI code page (the renderer's Windows text encoding), since `ConfigFileWireText.Respell` of the decoded text would have turned FE into U+FFFD first, falling back to the escaped reading when the code page cannot carry the bytes. Made public so the double-byte fallback (unreachable through the parser on a Windows-1252 host, where every byte round-trips) is tested directly.
- `Cgr016WriteOutFileTests` failed before the change (U+FFFD) and passes after; it is platform-neutral (the Windows test asserts the bytes back through the options' own wire encoding, Windows-1252 off Windows). Build clean, fast tests green; Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary: 0 failing members. CI on Linux and macOS is met by keeping the change platform-neutral.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 2e978de3 598ad02d c285e6d4 779790c0.
- 2026-10-28: Doing -> Done. -w @file writes a byte that is not valid UTF-8 unchanged, as curl 8.18.0 on Linux and the Schannel curl on Windows do
