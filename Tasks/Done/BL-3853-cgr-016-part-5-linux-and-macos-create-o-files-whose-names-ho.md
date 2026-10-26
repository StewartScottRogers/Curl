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
completed: 2026-10-26
---
# BL-3853 — CGR-016 part 5: Linux and macOS create -o files whose names hold invalid UTF-8 argument bytes under those raw bytes

## Goal

On Linux and macOS, `curl -s -o $'\xff.txt' URL` creates a file whose name is the bytes FF 2E 74 78 74, as curl 8.18.0 does (today EF BF BD .txt).

## Context

Split from BL-3545 (third-party gap report `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, CGR-016). On Linux, .NET hands `Main` an argv already decoded as UTF-8 with every invalid byte replaced by U+FFFD, so every option then re-encodes EF BF BD where curl 8.18.0 (OpenSSL, Linux) uses the raw byte (curl never re-encodes argv). Measure real curl in WSL with `Record-CurlExchange.ps1` before pinning output; no Python.

Needs BL-3849's lossless argument decoding: each invalid byte b arrives as the lone surrogate U+DC00+b. .NET's file APIs encode a path as UTF-8 and cannot name a file with an invalid byte, so opening such a name needs libc `open` through P/Invoke (AOT-safe, BCL only) with the raw bytes, wrapped in a `SafeFileHandle` and a `FileStream`; the `-o` open is in `Curl.Console/DeferredOutputFileStream.cs`. Use it only when the name holds an escaped byte, so every other name keeps today's path. The raw-byte open is an Integration test; the decision of when to use it is unit-testable. Check `--output-dir`, `-D` and `--create-dirs` as far as curl 8.18.0 shows a difference.

## Acceptance criteria

- [x] `-o $'\xff.txt'` writes a file with the same name bytes and contents as curl 8.18.0 on Linux; the measured command and both results are recorded in Notes.
- [x] Every other output file name, and every name on Windows, opens as before.
- [x] A focused unit test in `Curl.Console.UnitTests/Cgr016OutputFileTests.cs` fails before the change and passes after it on every platform.
- [x] Curl.Console stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Console`).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Plan: `RawByteFileName` (Curl.Console) opens an output file off Windows under its raw bytes when the name holds a surrogate U+DC80..U+DCFF (BL-3849's escape): `InvalidUtf8ByteEscape.Encoding` back to bytes, then the runtime's non-variadic `SystemNative_Open` from libSystem.Native (libc `open` is variadic, which a P/Invoke cannot call correctly on Apple silicon), wrapped in a `SafeFileHandle` and `FileStream`. `DeferredOutputFileStream.OpenAsync` (and `TryOpenNowAsync`, now through it) calls it; every other name and every name on Windows opens through `IFileSystem` as before. ADR-0822.
- Measured with `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1`, curl 8.18.0 (OpenSSL, Ubuntu): `curl -s -o $'\xff.txt' http://172.26.96.1:18853/` exit 0, `ls | od -An -tx1` gave `ff 2e 74 78 74`, contents `hello`. Curl before: EF BF BD .txt (the task's measurement). Curl after, a linux-x64 self-contained build run in WSL (`dotnet publish Curl.Console -r linux-x64 --self-contained -p:PublishAot=false`): `-o $'\xff.txt'` exit 0, name `ff 2e 74 78 74`, contents `hello`; again with `--no-clobber` gave `ff 2e 74 78 74 2e 31` (`.1`), contents `hello`.
- Also measured: `--output-dir $'\xfe' -o a.txt` writes `\xfe/a.txt` (covered here: the runner puts `--output-dir` in front of the name the stream opens); `-D $'\xfc.h'` writes `\xfc.h` and `--create-dirs -o $'\xfd/b.txt'` makes directory `\xfd`, both raw. Those go through `CurlCommandRunner` and `PhysicalOutputPaths`, outside this task's touches: filed as BL-3868.
- Decision: no Integration test for the native open: `Curl.Console.IntegrationTests` is outside touches and lanes run Windows only; the open is `[ExcludeFromCodeCoverage]` under ADR-0083 and was checked on Linux by hand as above. The decision, bytes, flags and errno mapping are unit-tested (`Cgr016OutputFileTests`, 14 tests, platform-neutral: no real file opened); they did not compile before the change, so they fail before it.
- Measure-CodeQuality.ps1 -Library Curl.Console: 100% line, 100% branch, 0 failing members (the first run flagged `StatusFor`'s complexity and the generated `byte[]` marshalling stub; fixed with a lookup table and a blittable `byte*` import reading `Marshal.GetLastSystemError`).
- dotnet build clean; fast tests: 46 test projects passed, 0 failed (Curl.Console.UnitTests 2914 passed). Tests are platform-neutral, so CI on Linux and macOS is covered by that.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 593440b5 ba29776f 7a4c0f9d 3ccadb6a 779790c0.
- 2026-10-26: Doing -> Done. Linux and macOS open -o, -O and -J files whose names hold invalid UTF-8 argument bytes under those raw bytes, as curl 8.18.0 does
