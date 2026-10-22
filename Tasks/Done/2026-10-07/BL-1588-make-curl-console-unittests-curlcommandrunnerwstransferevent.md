---
id: BL-1588
title: Make Curl.Console.UnitTests' CurlCommandRunnerWsTransferEventTests to CurlCompositionHttp2TraceTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1588 — Make Curl.Console.UnitTests' CurlCommandRunnerWsTransferEventTests to CurlCompositionHttp2TraceTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (11 files, 90 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CurlCommandRunnerWsTransferEventTests.cs`, `CurlCompositionAddressFamilyTests.cs`, `CurlCompositionAwsSigV4Tests.cs`, `CurlCompositionConnectOverrideTests.cs`, `CurlCompositionDnsServersTests.cs`, `CurlCompositionDnsTraceTests.cs`, `CurlCompositionDohSubTransferTraceTests.cs`, `CurlCompositionDohTests.cs`, `CurlCompositionFtpTraceTests.cs`, `CurlCompositionHaproxyProtocolTests.cs`, `CurlCompositionHttp2TraceTests.cs`.

## Context

- Split from BL-1461 (one task per range of files, as its Notes direct); BL-1461 keeps the whole-project checks and depends on this task. Follow BL-1461's Context: what matters in this project (command line, the scripted connector's script, request bytes, stdout and stderr, exit code), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1541 and BL-1542 show the pattern in `Curl.Networking.UnitTests`.
- "These classes' filter" below is one `FullyQualifiedName~Curl.Console.<Class>.` term per class the listed files declare (a partial class's files count once), joined with `|`.

## Acceptance criteria

- [x] `dotnet test Curl.Console.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Console.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the eleven files, before -> after: `Assert.` 186 -> 186, `[TestMethod` 90 -> 90, `[DataRow(` 102 -> 102. No assertion was changed or removed; `Diagnostics.Assert`/`Diff` lines sit beside the originals.
- Pattern as BL-1587: each class gets `TestContext` and a `Diagnostics` property; the shared helpers (`RunAsync`, `ConnectAsync`, `OpenTftpAsync`, `Parse`, `TracesFtp`/`TracesHttp2`, `HaproxyProtocolOf`) became instance methods that write the command line as `ARRANGE`, the run inside a `PHASE`, and exit code, error message, dialled targets, info lines and request/stdout/stderr (`BYTES`) as `ACT`. Expression-bodied tests became block bodies so they can write their result.
- The class filter ran 163 tests (the 90 methods with their data rows, including the `Integration` test in `CurlCompositionDnsServersTests`, which the filter also selects); every `END` line has non-zero arrange, act and assert counts, all passed.
- No test printed a `SLOW:` line.
- `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"`: 2661 passed, 24 skipped, 0 failed.
- Resumed on lane 6 by cherry-picking lane 8's commit 445f67879 unchanged. Rebuilt on the current branch: `dotnet build` clean, the whole fast suite passed (exit 0, 33 test projects; Curl.Console.UnitTests 2661 passed, 24 skipped), and the class filter again printed 163 `END` lines, none with a zero count and no `SLOW:` line. Lane 8's integration failures named no test and did not reproduce; most likely contention with the other lanes' runs.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 8 could not integrate: fast tests failed twice (no test named; then no test named) after rebasing onto the other lanes' work. The work is on branch factory/BL-1588-lane-8-20261007-111121; start with git cherry-pick --no-commit factory/BL-1588-lane-8-20261007-111121 and fix it.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the eleven Console test files writes ARRANGE, ACT and ASSERT diagnostics; 163 filtered tests pass with non-zero counts
