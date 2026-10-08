---
id: BL-1593
title: Make Curl.Console.UnitTests' ParallelHostQueueTests to RedirectPolicyMappingTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1593 — Make Curl.Console.UnitTests' ParallelHostQueueTests to RedirectPolicyMappingTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (10 files, 92 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `ParallelHostQueueTests.cs`, `ParallelProgressMeterTests.cs`, `ParallelRunTests.cs`, `ParallelTransferQueueTests.cs`, `PhysicalOutputPathsTests.cs`, `ProcessUserIdTests.cs`, `ProgramTests.cs`, `ProgressBarRecorderTests.cs`, `ProgressMeterFieldsTests.cs`, `RedirectPolicyMappingTests.cs`.

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

- Before (`Assert.` / `[TestMethod` / `[DataRow(` matches): ParallelHostQueueTests 14/9/5, ParallelProgressMeterTests 9/8/0, ParallelRunTests 18/9/0, ParallelTransferQueueTests 4/3/0, PhysicalOutputPathsTests 36/22/6, ProcessUserIdTests 5/5/3, ProgramTests 2/2/0, ProgressBarRecorderTests 27/19/6, ProgressMeterFieldsTests 4/4/34, RedirectPolicyMappingTests 25/11/4; total 144/92/58.
- After: 15/9/5, 9/8/0, 20/9/0, 4/3/0, 36/22/6, 5/5/3, 3/2/0, 27/19/6, 4/4/34, 25/11/4; total 148/92/58.
- The filtered detailed run printed 138 `END` lines (data rows included), none with a zero count. No test printed a `SLOW:` line, so no follow-up task.
- Choices: meter and bar text is printed with carriage returns and `Environment.NewLine` spelled out (`\r`, `\n`) so the lines read the same on every platform; `PhysicalOutputPathsTests` print paths as `<temporary root>/<name>`, never the machine's temp path. Shared helpers (`Map` in `RedirectPolicyMappingTests`, `NewBar`/`ActDrawn`/`AssertDrawn` in `ProgressBarRecorderTests`) write the lines every test there needs.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 92 tests in the ten files write ARRANGE, ACT and ASSERT diagnostics; build clean, fast tests green
