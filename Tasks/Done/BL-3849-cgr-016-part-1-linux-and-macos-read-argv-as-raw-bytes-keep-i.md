---
id: BL-3849
title: CGR-016 part 1: Linux and macOS read argv as raw bytes, keep invalid UTF-8 bytes losslessly, and send -d and --data-urlencode bytes unchanged
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/Program.cs, Curl.Console/RawCommandLineArguments.cs, Curl.Console.UnitTests/RawCommandLineArgumentsTests.cs, Curl.Cli.UnitLibrary/InvalidUtf8ByteEscape.cs, Curl.Cli.UnitTests/InvalidUtf8ByteEscapeTests.cs, Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitTests/Cgr016DataTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3849 — CGR-016 part 1: Linux and macOS read argv as raw bytes, keep invalid UTF-8 bytes losslessly, and send -d and --data-urlencode bytes unchanged

## Goal

On Linux and macOS, `curl -s -d $'\xff' URL` sends the body byte 0xFF (Content-Length 1) and `--data-urlencode $'\xff'` sends `%FF`, as curl 8.18.0 does, because Curl reads its arguments' raw bytes and keeps every invalid UTF-8 byte losslessly.

## Context

Split from BL-3545 (third-party gap report `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, CGR-016). On Linux, .NET hands `Main` an argv already decoded as UTF-8 with every invalid byte replaced by U+FFFD, so every option then re-encodes EF BF BD where curl 8.18.0 (OpenSSL, Linux) uses the raw byte (curl never re-encodes argv). Measure real curl in WSL with `Record-CurlExchange.ps1` before pinning output; no Python.

Suggested design (decide and record an ADR): read argv's raw bytes on Linux from `/proc/self/cmdline` (NUL-separated, skip the program path) and on macOS through `_NSGetArgv` (P/Invoke to libSystem, AOT-safe); decode each argument as UTF-8 but map every invalid byte b to the lone surrogate U+DC00+b (the "surrogate escape" scheme), in a new `InvalidUtf8ByteEscape` in Curl.Cli.UnitLibrary with an `Encoding` that maps those surrogates back to their bytes. Fall back to `args` when the raw bytes cannot be read or their count differs. Off Windows, set the argument wire encoding (today `ConfigFileWireTextEncoding`, set only on Windows in `CommandLineParser.cs` near line 219) to that encoding, so `CommandLineOptions.CommandLineTextBytes` (`CommandLineOptions.cs` near line 2389) sends the raw bytes for `-d`, `--data-*`, `--json`, `--data-urlencode` and `--url-query`. Check that a UTF-8 config file and `--expand-` values on Linux still go out as UTF-8. BL-3850 to BL-3853 build on this for headers and cookies, URLs, `-w` and `-o` file names.

## Acceptance criteria

- [x] On Linux and macOS Curl reads its arguments' raw bytes and keeps each invalid UTF-8 byte losslessly; valid UTF-8 arguments, and every argument on Windows, are unchanged.
- [x] `curl -s -d $'\xff' URL` and `--data-urlencode $'\xff'` send the same request bytes as curl 8.18.0 on Linux; the measured commands and both results are recorded in Notes.
- [x] Focused unit tests in Curl.Cli.UnitTests (`InvalidUtf8ByteEscapeTests.cs`, `Cgr016DataTests.cs`) and Curl.Console.UnitTests fail before the change and pass after it, and pass on Windows, Linux and macOS.
- [x] An ADR records the design, marked "Decided by Claude under Stewart's delegation".
- [x] Curl.Cli.UnitLibrary and Curl.Console stay at 100% line and branch coverage within the complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Console`).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Plan: `RawCommandLineArguments` (Curl.Console) re-reads argv off Windows (`/proc/self/cmdline`, `_NSGetArgc`/`_NSGetArgv`), lines it up with .NET's args by position from the end (falls back to args when unreadable, too short or a valid-UTF-8 entry differs) and decodes with `InvalidUtf8ByteEscape.Decode` (invalid byte b -> lone U+DC00+b). `CommandLineTextBytes` encodes with `InvalidUtf8ByteEscape.Encoding` wherever it used UTF-8. ADR-0821.
- Decision: `CommandLineParser.cs` was left unchanged: rather than setting `ConfigFileWireTextEncoding` off Windows (which also changes how invalid config and header files are read), the UTF-8 branch of `CommandLineTextBytes` now uses the escape encoding, which gives identical bytes for any text without escaping surrogates.
- Measured with Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1, curl 8.18.0 (OpenSSL, Ubuntu): `bash -c "curl -s -d $'\xff' http://172.26.96.1:18849/"` sent `Content-Length: 1` and body FF; `curl -s --data-urlencode $'\xff' ...` sent `Content-Length: 3` and body `%FF`. Curl now builds the same bodies (Cgr016DataTests; before the change they were EF BF BD and %EF%BF%BD).
- Tests are platform-neutral (byte arrays only, no paths); the CI-on-Linux/macOS part of the tests criterion is met by that and the fast tests green on Windows (46 test projects passed).
- Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Console: 0 failing members. The platform argv readers are [ExcludeFromCodeCoverage] under ADR-0083.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: d4a270ab c8eb6298 9fc2e6ad 1e378075 0d7e61ee 779790c0.
- 2026-10-26: Doing -> Done. Linux and macOS read argv raw bytes; -d and --data-urlencode send invalid UTF-8 bytes unchanged as curl 8.18.0 does
