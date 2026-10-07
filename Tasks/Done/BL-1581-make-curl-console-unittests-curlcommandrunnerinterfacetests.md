---
id: BL-1581
title: Make Curl.Console.UnitTests' CurlCommandRunnerInterfaceTests to CurlCommandRunnerNoClobberTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1581 — Make Curl.Console.UnitTests' CurlCommandRunnerInterfaceTests to CurlCommandRunnerNoClobberTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (8 files, 99 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CurlCommandRunnerInterfaceTests.cs`, `CurlCommandRunnerLibcurlTests.cs`, `CurlCommandRunnerLiveProgressMeterTests.cs`, `CurlCommandRunnerMaxTimeTests.cs`, `CurlCommandRunnerNetrcTests.cs`, `CurlCommandRunnerNextGroupTests.cs`, `CurlCommandRunnerNoBufferTests.cs`, `CurlCommandRunnerNoClobberTests.cs`.

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

- Counts in the 8 files, before -> after: `Assert.` 230 -> 230 (`Diagnostics.Assert(` does not match the pattern), `[TestMethod` 99 -> 99, `[DataRow(` 48 -> 48. Existing assertions are untouched; some inline expected values moved into locals so the diagnostic and the real assertion share them, and the private run helpers became `async` so each run sits inside `Phase("run")` and writes the command line as ARRANGE and exit code, stdout and stderr as ACT.
- `RunAsync_CancelledBeforeMaxTime_LetsTheCancellationOut`: its only assertion is `Assert.ThrowsExactlyAsync`, which also runs the code, so its ASSERT line follows that call (choice: no other place exists without changing the test).
- The filtered detailed run: 126 tests, 125 passed, 1 skipped (`RunAsync_OffWindows_LooksForDotNetrcOnly`, off-Windows only); 125 `END` lines, none with a zero arrange, act or assert count. Text is printed LF-normalised and no temporary path is printed, so the log reads the same on every OS.
- No test printed a `SLOW:` line.
- `dotnet build Curl.Console.UnitTests -warnaserror` clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"`: 2661 passed, 24 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 99 tests in the 8 Curl.Console.UnitTests files write ARRANGE, ACT and ASSERT diagnostics
