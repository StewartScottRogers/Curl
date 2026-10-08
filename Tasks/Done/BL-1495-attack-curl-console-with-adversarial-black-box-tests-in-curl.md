---
id: BL-1495
title: Attack Curl.Console with adversarial black-box tests in Curl.Console.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1443, BL-1445, BL-1447, BL-1453, BL-1454, BL-1461]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1495 — Attack Curl.Console with adversarial black-box tests in Curl.Console.UnitTests

## Goal

`Curl.Console.UnitTests` gains adversarial black-box tests that attack `Curl.Console`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1443, BL-1445, BL-1447, BL-1453, BL-1454, BL-1461.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Console`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: the whole executable as curl's drop-in: exit code and stderr bytes for invalid command lines, closed or redirected stdin and stdout, output to an existing, read-only or directory path, `-w` with every variable on a failed transfer, and repeated runs from parallel processes; every expected answer measured from real curl with `Record-CurlExchange.ps1`.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Console.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Console.UnitTests/` and this task file.

## Notes

- New file `Curl.Console.UnitTests/CurlCommandRunnerAdversarialCommandLineTests.cs`, 13 test methods (19 cases), command line in, stdout/stderr/exit code out, a recording fake handler only - no socket. Every expected answer was measured from curl 8.21.0 (mingw, Schannel) on 2026-10-07 by running it against `http://127.0.0.1:1/`; the refusals happen before any connection, so `Record-CurlExchange.ps1`'s server was not needed.
- Boundaries: `RunAsync_RetryAtIntMaxValue_RunsTheTransfer` (2147483647 accepted), `RunAsync_NumericOptionWithValueOutsideItsValidPartition_...` with `--retry 2147483648` (one past), `RunAsync_RetryJustBelowZero_PrintsPositiveNumericalParameterAndExitsFailedInit`, `RunAsync_PortOnePastTheLargest_PrintsUrlRejectedAndExitsUrlMalformat` (65536).
- Malformed input: `RunAsync_LongOptionWithEqualsAndNoValue_NamesTheWholeArgumentAndExitsFailedInit`, `RunAsync_BlankUrlArgument_PrintsBlankArgumentAndExitsFailedInit`, `RunAsync_WriteOutConsumingTheOnlyUrl_PrintsNoUrlSpecifiedAndExitsFailedInit`, `RunAsync_UserInfoWithEmptyHostAndSecondAt_PrintsUrlRejectedAndExitsUrlMalformat`, `RunAsync_UnknownLongOption_PrintsIsUnknownAndExitsFailedInit`.
- Invalid partitions: `RunAsync_NumericOptionWithValueOutsideItsValidPartition_PrintsProperNumericalParameterAndExitsFailedInit` (wrong type `abc`/`x`, wrong format `1.5`, below range `-1`/`-2`, above range, across `--max-time`, `--connect-timeout`, `--expect100-timeout`, `--max-filesize`, `--max-redirs`, `--retry`), `RunAsync_ProtoWithOnlyAnUnknownProtocol_WarnsThenPrintsBadlyUsedAndExitsFailedInit`, `RunAsync_UnknownScheme_PrintsProtocolNotSupportedAndExitsUnsupportedProtocol`.
- State and concurrency: `RunAsync_SameRefusedCommandLineFromManyRunnersAtOnce_EveryRunnerAnswersAsOneRunAlone` (16 runners on parallel tasks, each answers exactly as one alone). Parallel OS processes, closed stdin/stdout and read-only paths need the real executable and disk; they belong in an IntegrationTests project, outside this task's `touches`, and closed stdout is already covered by `ClosedStandardOutputStreamTests`.
- Defects found: none - every attack matched real curl byte for byte, so no follow-up task was filed.
- Test count (fast run, `Curl.Console.UnitTests`): before 2690, after 2709 (2685 passed, 24 skipped).

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. 19 adversarial command-line tests attack Curl.Console against real curl's measured answers; no defect found
