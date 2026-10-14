---
id: BL-2172
title: Config file unquoted-whitespace warning matches curl 8.22.0 (upstream test459, test1712)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2172 — Config file unquoted-whitespace warning matches curl 8.22.0 (upstream test459, test1712)

## Goal

The warning Curl prints for a config-file option whose argument has unquoted whitespace is byte for byte curl 8.22.0's, so upstream test459 and test1712 pass.

## Context

Split from BL-2160 (GF-0038, curl 8.22.0 changed behaviour). Measured 2026-10-13 through the ratchet: both cases fail on `<verify><stderr>` at byte 241 of line 1; the line starts the same ("Warning: <dir>/config:1 Option 'data' uses argument with unquoted whitespace. This may ca...") and differs after it. Read the expected text in `Curl.Conformance.UnitTests/UpstreamTestData/test459.curltest` and `test1712.curltest`. The warning is written in `Curl.Cli.UnitLibrary/ConfigFileSyntax.cs`; its tests are in `Curl.Cli.UnitTests/CommandLineConfigFileTests.cs`. Debug one case with `CURL_UPSTREAM_CASE` (see `Curl.Conformance.UnitTests/CLAUDE.md`).

## Acceptance criteria

- [x] test459 and test1712 pass and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in `Curl.Cli.UnitTests` pins the new warning text (ADR-0482).
- [x] Upstream test320, test1062, test1498, test2059 and test2318, which BL-2160 measured passing on 2026-10-13 but unlisted, are on `PassingUpstreamCases.txt` (or, for any that no longer passes, its cause is filed as a task).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.22.0 (Git for Windows `ucrt64\bin\curl.exe`, 2026-10-13) with a config file under a ~150-byte path: the warning text is unchanged from 8.21.0; what changed is `COLUMNS`. `COLUMNS=10000` keeps the warning on one line and `COLUMNS=10001` wraps it at 79, so 8.22.0's `get_terminal_columns` accepts 21 to 10000 where 8.21.0 accepted 21 to 9999. Both upstream cases set `COLUMNS=10000`.
- Three fixes, all needed for test459 and test1712:
  1. `Curl.Cli.WrappedMessage.TerminalColumns` and `Curl.Console.TerminalColumns.TryParseColumnsVariable` accept 10000.
  2. `ConfigFileSyntax.ReadLines` wraps its two warnings at the parse's own `COLUMNS` (`options.ReadEnvironmentVariable`), not this process's, as `ConfigFileApplier` already did for `cannot read config from`; the console rewraps each warning line but cannot join lines the parser had already cut at 79.
  3. The harness learns curl 8.22.0's `<stderr mode="warn">` from `runtests.pl`: on both sides each line loses its first `Warning: ` and first CR and its LF becomes a space (`UpstreamTestSectionLineEndings.NormalizeWarnings`). Without it the CRLF Curl writes on Windows differs from the expected LF.
- Touches widened to `Curl.Console`, `Curl.Console.UnitTests`, `Curl.Conformance.UnitLibrary` and `Curl.Conformance.UnitTests` (the whole project rather than only `PassingUpstreamCases.txt`): the console's own 9999 cap and the harness's `mode="warn"` are part of the same fix. No task in Doing on `origin/work/dark-factory` named any of them.
- Focused unit tests: `CommandLineConfigFileTests.Parse_ConfigFileWarningAtTheParsesColumnsVariable_KeepsItOnOneLine` (both warnings), and rows for 10000 and 10001 in `WrappedMessageTests` and `TerminalColumnsTests`; `UpstreamCaseVerificationTests.FindFirstDifference_StderrInWarnMode_ComparesWrappedWarningsAsOneLine` covers the harness.
- test320, test1062, test1498, test2059 and test2318 still pass (each run alone through `CURL_UPSTREAM_CASE`) and are listed along with 459 and 1712; the full fast run then held the ratchet. No ADR: the change matches measured 8.22.0 behaviour under ADR-0479's retarget. Measure-CodeQuality was not run: every new branch is reached by the tests above.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Config file warnings stay on one line at COLUMNS=10000 as curl 8.22.0 prints them; harness handles mode=warn; test459, test1712 and five more cases listed
