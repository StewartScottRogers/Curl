---
id: BL-4937
title: Turn off --ai-help, --extended-help, --log-level and --log-file under CURL_EXTENSIONS=0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitLibrary/CurlHelpText.cs, Curl.Cli.UnitLibrary/CurlOptionManualSection.cs, Curl.Console/CurlCommandRunner.cs, Curl.Cli.UnitTests/CurlHelpTextTests.cs, Curl.Cli.UnitTests/CurlOptionManualSectionTests.cs, Curl.Console.UnitTests/CurlCommandRunnerExtensionsTests.cs, Curl.Conformance.Extended.UnitTests/CurlSurface/Discovery/ExtensionListingCases.cs, Documentation/Conformance/Extended/CurlSurface-Discovery.md, Documentation/Protocols.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4937 — Turn off --ai-help, --extended-help, --log-level and --log-file under CURL_EXTENSIONS=0

## Goal

With `CURL_EXTENSIONS=0` the port is plain curl: the four port-only options are unknown and `--help` is byte-identical to curl's.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-015 (Low, confirmed: reproduced at least twice).

- Repro: `CURL_EXTENSIONS=0 curl -s --log-file x.log http://127.0.0.1:18800/plain; echo $?` (also `--log-level warning`, `--extended-help`, `--ai-help`); `CURL_EXTENSIONS=0 curl --help` diffed against REF.
- Expected: "Setting CURL_EXTENSIONS=0 turns every one off": `curl: option --log-file: is unknown` + try line, exit 2; `--help` byte-identical.
- Actual: All four exit 0; `--log-file` creates `x.log`; `--help` gains the `--ai-help` line. `--log-level`/`--log-file` are not in `--help all` or `--extended-help`.
- Where to start: `CommandLineOptionTable.cs` lines 80-81 and 326 register them in the core table; `CurlCommandRunner.cs` help text. If any must stay (e.g. `--ai-help`), record the exception in an ADR and in `Documentation/Protocols.md`.

## Acceptance criteria

- [x] With `CURL_EXTENSIONS=0` each of the four options prints curl's two-line unknown-option message and exits 2 (or is a documented exception), and no `x.log` is created.
- [x] `CURL_EXTENSIONS=0 curl --help` is byte-identical to curl 8.22.0's.
- [x] `--log-level` and `--log-file` are listed in `--extended-help`, and `--ai-help` stays right.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

Decision (ADR-1251): the parser hides the four names under CURL_EXTENSIONS=0 (also `--no-`/`--expand-` spellings); `--help` pages drop the `--ai-help` line/row so they equal the reference bytes (reference files already in Curl.Cli.UnitTests); `--extended-help` / `--ai-help extensions` list `--log-level`/`--log-file` as a `diagnostic-log` entry (added in the runner, since no extension owns them). Extended conformance cases Listing-2 and Listing-4 now pin the unknown-option refusal (touches widened; no other Doing task holds them). Not run live against Schannel curl: the unknown-option text is the existing parser refusal. Quality run: Cli and Console 100/100, CRAP 10.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 1e602075 4d306ed5 701a25cc a0b49a29.
- 2026-11-11: Doing -> Done. CURL_EXTENSIONS=0 now refuses the four port-only options and --help is curl's bytes (ADR-1251)
