---
id: BL-3893
title: CGR-015 part 1b: on Windows, write stderr text that echoes an argument in the ANSI code page
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3892]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Console.UnitTests/Cgr015StderrEchoTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3893 — CGR-015 part 1b: on Windows, write stderr text that echoes an argument in the ANSI code page

## Goal

On Windows the stderr text that echoes argument text (such as `curl: option --sílent: is unknown`) is written in the system ANSI code page, as the reference curl does.

## Context

Split from BL-3843 (itself split from BL-3544, CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3544 for repros). Depends on BL-3892, which converts argv and records the ADR. The runner writes stderr as UTF-8 today (`Curl.Console/CurlCommandRunner.cs` around lines 1294 and 5221); `Curl.Cli.UnitLibrary/CommandLineRefusal.cs` builds the refusal text. Encoding comes from `ConfigFileWireText.WindowsAnsiCodePage`; tests inject cp1252 (`CodePagesEncodingProvider`) so they pass on Linux and macOS. Measure real curl with `Record-CurlExchange.ps1` before pinning bytes. Off Windows nothing changes (UTF-8).

## Acceptance criteria

- [x] On Windows `curl --sílent <url>` writes the unknown-option line with byte E9, as real curl does (command and both results in Notes).
- [x] A focused unit test in `Curl.Console.UnitTests/Cgr015StderrEchoTests.cs` fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [x] Off Windows (UTF-8) the same refusal writes the same bytes as before.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

Split from BL-3843, which was over its cost cap.

- Carried over BL-3843 stash 5c613a00's standard-error half (`CommandLineRefusal.EchoesConfigFileText`, the
  runner's `WriteRefusalLinesAsync` and encoding-taking `WriteErrorLinesAsync`/`WriteErrorLineAsync`/
  `WriteErrorPiecesAsync` overloads), resolved against the runner's current `LoadTlsSessionFileLines`.
- Encoding: `CredentialEncoding.ForPlatform(runsOnWindows)`, which the runner already uses for header text
  (the system ANSI code page, Windows-1252 where the host names none). A refused config file line stays
  UTF-8, as curl echoes the file's own bytes. Recorded in ADR-0840.
- Measured 2026-10-26, `Record-CurlExchange.ps1 -NoServer`, reference ucrt64 curl 8.22.0, and this build
  (`dotnet run --no-build --project Curl.Console`, as `bin/` is outside the lane's reach); identical:
  - `curl --sélent http://127.0.0.1:1/`: exit 2, `curl: option --s` E9 `lent: is unknown` CR LF (both).
  - `curl --sílent http://127.0.0.1:1/` (the task's spelling): exit 2, byte ED, as í is ED in cp1252 (both);
    the criterion's E9 is `é`, which the tests use.
- `Cgr015StderrEchoTests`: with the echo encoding put back to UTF-8 (the old behaviour) the Windows test
  failed (1 failed, 2 passed); with the change all 3 pass. They compare against `ForPlatform(true)` with
  `runsOnWindows` injected, so they pass on Linux and macOS; off Windows the bytes are UTF-8 as before.
- `dotnet build` clean, fast tests green (46 projects), `Measure-CodeQuality.ps1 -Library
  Curl.Console,Curl.Cli.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 743743ab d97c2aa5 080c93f1 326de9ca.
- 2026-10-26: Doing -> Done. On Windows a refused command line echoes argument text in the ANSI code page (--sélent as E9), matching reference curl; config file lines and other platforms stay UTF-8
