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
completed: 2026-10-26
---
# BL-3852 — CGR-016 part 4: Linux and macOS write -w format bytes that are invalid UTF-8 unchanged

## Goal

On Linux and macOS, `curl -s -o /dev/null -w $'\xfe' URL` writes the single byte 0xFE to stdout, as curl 8.18.0 does (today EF BF BD).

## Context

Split from BL-3545 (third-party gap report `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, CGR-016). On Linux, .NET hands `Main` an argv already decoded as UTF-8 with every invalid byte replaced by U+FFFD, so every option then re-encodes EF BF BD where curl 8.18.0 (OpenSSL, Linux) uses the raw byte (curl never re-encodes argv). Measure real curl in WSL with `Record-CurlExchange.ps1` before pinning output; no Python.

Needs BL-3849's lossless argument decoding: each invalid byte b arrives as the lone surrogate U+DC00+b. `Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs` renders the template's literal text; write such a surrogate as its byte rather than U+FFFD's UTF-8. Check `-w @file` and `%output{}` file names too.

## Acceptance criteria

- [x] `-o /dev/null -w $'\xfe'` writes the same stdout bytes as curl 8.18.0 on Linux; the measured command and both results are recorded in Notes.
- [x] Valid UTF-8 `-w` text, and every `-w` text on Windows, is written as before.
- [x] A focused unit test in `Curl.Output.UnitTests/Cgr016WriteOutTests.cs` fails before the change and passes after it on every platform.
- [x] Curl.Output.UnitLibrary stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary`).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured with `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1 -Port 18852`, curl 8.18.0 (OpenSSL, Ubuntu): `bash -c "curl -s -o /dev/null -w $'\xfe' http://172.26.96.1:18852/ | od -An -tx1"` printed ` fe` (one byte FE). Curl before the change wrote EF BF BD for the escaped argument U+DCFE; it now writes FE (`Cgr016WriteOutTests.RenderAsync_EscapedInvalidByte_WritesTheByte`).
- Change: `WriteOutTemplateRenderer.Encode` treats U+DC80 to U+DCFF like the marked header bytes (one `SearchValues<char>`), writing each as its low byte; valid UTF-8 text and every other lone surrogate are written as before (`RenderAsync_ValidUtf8Only_WritesUtf8AsBefore`, `RenderAsync_LoneSurrogateOutsideTheEscapes_WritesReplacementCharacter`).
- Decision (ADR-0822): no platform switch. On Windows argv is never escaped (BL-3849 decodes only off Windows), so a Windows `-w` text holds such a surrogate only from ill-formed UTF-16 no shell passes; every `-w` text a Windows shell can pass is written as before.
- `-w @file` still decodes the file as plain UTF-8 in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` (outside this task's touches, and it changes Windows too): filed as BL-3862. `%output{}` names holding an escaped byte are opened by `Curl.Console/DiskWriteOutFileOpener.cs` with U+FFFD: filed as BL-3863 (after BL-3853's raw-byte file names). `%output{}` targets do get the raw bytes in their contents (`RenderAsync_EscapedByteAfterOutputFile_WritesTheByteToTheFile`).
- Tests are platform-neutral (in-memory streams, byte arrays, no paths); the CI-on-Linux/macOS part is met by that and the fast tests green on Windows (46 test projects passed, 0 failed). `dotnet build`: 0 warnings, 0 errors.
- `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 6fd990b3 fa400498 03933128 f9e9638a 779790c0.
- 2026-10-26: Doing -> Done. Linux and macOS -w writes argument bytes that are invalid UTF-8 unchanged (-w $'\xfe' writes FE as curl 8.18.0)
