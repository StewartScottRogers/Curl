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
completed:
---
# BL-3893 — CGR-015 part 1b: on Windows, write stderr text that echoes an argument in the ANSI code page

## Goal

On Windows the stderr text that echoes argument text (such as `curl: option --sílent: is unknown`) is written in the system ANSI code page, as the reference curl does.

## Context

Split from BL-3843 (itself split from BL-3544, CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read BL-3544 for repros). Depends on BL-3892, which converts argv and records the ADR. The runner writes stderr as UTF-8 today (`Curl.Console/CurlCommandRunner.cs` around lines 1294 and 5221); `Curl.Cli.UnitLibrary/CommandLineRefusal.cs` builds the refusal text. Encoding comes from `ConfigFileWireText.WindowsAnsiCodePage`; tests inject cp1252 (`CodePagesEncodingProvider`) so they pass on Linux and macOS. Measure real curl with `Record-CurlExchange.ps1` before pinning bytes. Off Windows nothing changes (UTF-8).

## Acceptance criteria

- [ ] On Windows `curl --sílent <url>` writes the unknown-option line with byte E9, as real curl does (command and both results in Notes).
- [ ] A focused unit test in `Curl.Console.UnitTests/Cgr015StderrEchoTests.cs` fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [ ] Off Windows (UTF-8) the same refusal writes the same bytes as before.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

Split from BL-3843, which was over its cost cap.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 743743ab d97c2aa5 080c93f1 326de9ca.
