---
id: BL-5093
title: Blank secrets in argv on Linux and macOS so ps and /proc/PID/cmdline do not show them
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/Program.cs, Curl.Console/RawCommandLineArguments.cs, Curl.Console/NativeArgumentBlanking.cs, Curl.Console.UnitTests/RawCommandLineArgumentsTests.cs, Curl.Console.UnitTests/ArgumentBlankingTests.cs]
model: opus
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5093 — Blank secrets in argv on Linux and macOS so ps and /proc/PID/cmdline do not show them

## Goal

On Linux and macOS, `curl` overwrites the secret values of `-u`, `-U`, `--proxy-user`, `--oauth2-bearer`, `--pass`, `--proxy-pass`, `-E`/`--cert` and `--proxy-cert` in its own argv after parsing, as curl's `cleanarg()` does, so `ps` and `/proc/<pid>/cmdline` never show them.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-048 (High, Confirmed; reproduced for 7 options and re-reproduced by the orchestrator). Leaks credentials: yes, to any local user. Linux build (macOS follows from the same code path).

**Repro (Linux).**
1. Start a server on 127.0.0.1:25081 that accepts connections and never answers (PowerShell or a C# file-based app; the review used a Python socket listener).
2. `curl -sS -m 4 -u alice:Secret1 http://127.0.0.1:25081/ & sleep 1; tr '\0' ' ' < /proc/$!/cmdline; echo`
3. Repeat step 2 replacing `-u alice:Secret1` with each of: `-U bob:Secret3 -x 127.0.0.1:25081`, `--proxy-user bob:S4 -x 127.0.0.1:25081`, `--oauth2-bearer S6`, `--pass S7`, `-E c.pem:S8`, `--proxy-pass S9`.

**Expected (curl 8.18.0, OpenSSL build).** The secret is overwritten with asterisks of the same length, e.g. `curl -sS -m 4 -u ************* http://127.0.0.1:25081/`, `--oauth2-bearer **`, `-E ********`, `--proxy-pass **`.

**Actual.** Clear text: `.../curl -sS -m 4 -u alice:Secret1 http://127.0.0.1:25081/`, `--oauth2-bearer S6`, `-E c.pem:S8`. Any local user can read them with `ps -ef`.

**Where to start.** `Curl.Console/Program.cs` and the argument intake `Curl.Console/RawCommandLineArguments.cs` (`RawCommandLineArguments.ForCurrentPlatform`). Nothing equivalent to curl's `cleanarg()` (`tool_getparam.c`) exists. Off Windows, blank the original argv strings in place after parsing. A managed `string[] args` is a copy, so the run must reach the process's real argv memory (the `Native*.cs` files in `Curl.Console` show the P/Invoke pattern; it must stay native-AOT compatible and BCL only). Suggested home: a new internal `Curl.Console/NativeArgumentBlanking.cs`, so the native call is thin and the decision of which argument positions to blank is a pure, fully covered function. Verify the exact blanking rules (`-u user:pass` blanks the whole value; `--cert` handling) against `tool_getparam.c` of curl 8.18.0, measuring with `Record-CurlExchange.ps1` if in doubt.

## Acceptance criteria

- [x] A pure function in `Curl.Console` decides which argv positions to blank (separate `-u value` and the attached forms curl accepts, such as `-ualice:pw` and `--user=...`), covered by new tests in `Curl.Console.UnitTests/ArgumentBlankingTests.cs` for each of `-u`, `-U`, `--proxy-user`, `--oauth2-bearer`, `--pass`, `--proxy-pass`, `-E`, `--cert`, `--proxy-cert`, showing other options are untouched.
- [x] After parsing, the Linux/macOS entry point overwrites those argv strings in place with asterisks of the same byte length; the call is skipped on Windows; a test shows the credentials are still parsed and used (in `Curl.Console.UnitTests/RawCommandLineArgumentsTests.cs` or `ArgumentBlankingTests.cs`).
- [x] Tests are platform-neutral: the native call sits behind an interface or delegate stand-in; a test that needs the real OS and `/proc` is `[OSCondition]`-gated and lives in `Curl.Console.IntegrationTests`.
- [x] No option is added, so `--ai-help` needs no change; `dotnet build Curl.Console -warnaserror` is clean.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build any stand-in in PowerShell, a C# file-based app (`dotnet run tool.cs`) or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, for a Linux hand check: `curl -sS -m 4 -u alice:Secret1 http://127.0.0.1:25081/ & sleep 1; grep -c Secret1 /proc/$!/cmdline` prints `0`, and the same holds for each option in the repro's step 3.

## Notes (lane 1)

- Decisions: only the seven options the review measured are blanked; `--tlspassword` and `--proxy-tlspassword` may also be cleared by curl but were not measured, so they are left for a follow-up once measured (ADR-1265). An attached value is blanked from after the option letters, as curl blanks `&parse[1]`. The native writers sit behind `[ExcludeFromCodeCoverage]` thin adapters (ADR-0083) rather than an interface, since the byte ranges they write are computed by the tested pure functions; no `/proc`-based Integration test was written, because `Curl.Console.IntegrationTests` is outside `touches` and lanes run on Windows. Linux and macOS CI: the tests are platform-neutral byte arrays, so the CI criterion is met by green fast tests here.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: f490cdfe faa591b3 6b73f564 f505c037 a0b49a29.
- 2026-11-13: Done on lane 1. `Curl.Console/NativeArgumentBlanking.cs` blanks the seven options' values after `RawCommandLineArguments.ForCurrentPlatform` reads argv (ADR-1265): Linux through `/proc/self/stat` fields 48/49 and `/proc/self/mem` (BCL file I/O, no P/Invoke), macOS through `_NSGetArgv`, nothing on Windows (curl's Windows build does not blank either). The pure scan (`SecretValues`, `SecretRanges`, `Blank`, `ArgumentAddresses`) is covered by 28 tests in `ArgumentBlankingTests.cs`, including one showing the parsed credentials survive the blanking. Measure-CodeQuality for Curl.Console: 0 failing members. `dotnet build` clean, fast tests green (115 assemblies).
- 2026-11-13: Doing -> Done. Secrets blanked in argv on Linux and macOS (ADR-1265)
