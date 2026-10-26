---
id: BL-3853
title: CGR-016 part 5: Linux and macOS create -o files whose names hold invalid UTF-8 argument bytes under those raw bytes
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3849]
touches: [Curl.Console/DeferredOutputFileStream.cs, Curl.Console/RawByteFileName.cs, Curl.Console.UnitTests/Cgr016OutputFileTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3853 — CGR-016 part 5: Linux and macOS create -o files whose names hold invalid UTF-8 argument bytes under those raw bytes

## Goal

On Linux and macOS, `curl -s -o $'\xff.txt' URL` creates a file whose name is the bytes FF 2E 74 78 74, as curl 8.18.0 does (today EF BF BD .txt).

## Context

Split from BL-3545 (third-party gap report `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, CGR-016). On Linux, .NET hands `Main` an argv already decoded as UTF-8 with every invalid byte replaced by U+FFFD, so every option then re-encodes EF BF BD where curl 8.18.0 (OpenSSL, Linux) uses the raw byte (curl never re-encodes argv). Measure real curl in WSL with `Record-CurlExchange.ps1` before pinning output; no Python.

Needs BL-3849's lossless argument decoding: each invalid byte b arrives as the lone surrogate U+DC00+b. .NET's file APIs encode a path as UTF-8 and cannot name a file with an invalid byte, so opening such a name needs libc `open` through P/Invoke (AOT-safe, BCL only) with the raw bytes, wrapped in a `SafeFileHandle` and a `FileStream`; the `-o` open is in `Curl.Console/DeferredOutputFileStream.cs`. Use it only when the name holds an escaped byte, so every other name keeps today's path. The raw-byte open is an Integration test; the decision of when to use it is unit-testable. Check `--output-dir`, `-D` and `--create-dirs` as far as curl 8.18.0 shows a difference.

## Acceptance criteria

- [ ] `-o $'\xff.txt'` writes a file with the same name bytes and contents as curl 8.18.0 on Linux; the measured command and both results are recorded in Notes.
- [ ] Every other output file name, and every name on Windows, opens as before.
- [ ] A focused unit test in `Curl.Console.UnitTests/Cgr016OutputFileTests.cs` fails before the change and passes after it on every platform.
- [ ] Curl.Console stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Console`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 593440b5 ba29776f 7a4c0f9d 3ccadb6a 779790c0.
