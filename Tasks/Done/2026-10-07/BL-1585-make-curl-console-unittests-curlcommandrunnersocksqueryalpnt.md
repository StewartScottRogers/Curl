---
id: BL-1585
title: Make Curl.Console.UnitTests' CurlCommandRunnerSocksQueryAlpnTraceTests to CurlCommandRunnerTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1585 — Make Curl.Console.UnitTests' CurlCommandRunnerSocksQueryAlpnTraceTests to CurlCommandRunnerTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (10 files, 126 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CurlCommandRunnerSocksQueryAlpnTraceTests.cs`, `CurlCommandRunnerSpeedLimitTests.cs`, `CurlCommandRunnerSshOptionTests.cs`, `CurlCommandRunnerStandardErrorFileTests.cs`, `CurlCommandRunnerStandardOutputDashTests.cs`, `CurlCommandRunnerStandardOutputFailureTests.cs`, `CurlCommandRunnerStartedTransferProgressMeterTests.cs`, `CurlCommandRunnerStyledOutputTests.cs`, `CurlCommandRunnerTcpIoTraceTests.cs`, `CurlCommandRunnerTests.cs`.

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

- Counts in the 10 files, before -> after: `Assert.` 290 -> 290, `[TestMethod` 126 -> 126, `[DataRow(` 55 -> 55. Some multi-line expected values moved into locals so the `Diff` line and the assertion share them; the asserted values are unchanged.
- The filtered detailed run: 163 tests ran, 163 `END` lines, none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line (the whole filtered run took under 8 s), so no follow-up task.
- Shared run helpers write ARRANGE (arguments, scripted responses or handler inputs), a `run` PHASE and ACT (exit code, stdout, stderr) lines. Text is LF-normalised; control-byte output (styled output, stdout dash) goes through `BYTES`; the temp path in SshOption tests prints as `<temp>/`; argument and stderr text over 200 characters is shortened in CurlCommandRunnerTests (the 65 KB hostname tests), so nothing printed depends on the OS or floods the log.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 10 files write diagnostics; 163 END lines, none zero; build clean, fast tests green.
