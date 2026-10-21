---
id: BL-1596
title: Make Curl.Console.UnitTests' TransferDiagnosticLinesTests to WriteGateTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1596 — Make Curl.Console.UnitTests' TransferDiagnosticLinesTests to WriteGateTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (10 files, 87 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `TransferDiagnosticLinesTests.cs`, `TransferProgressRecorderTests.cs`, `TransferProxySelectionPreProxyTests.cs`, `TransferTimersTests.cs`, `UrlEffectiveTests.cs`, `UrlTransferTests.cs`, `WarningLineWrapperTests.cs`, `WindowsOutputFileNameSanitizerTests.cs`, `WriteGateStreamTests.cs`, `WriteGateTests.cs`.

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

- Pattern: each class gets `TestContext` and `Diagnostics`; shared helpers (`Started`, `Select`/`Parse`, `Normalize`, `Create`/`Parse`, `WrapText`/`WrapLine`) write the command line or inputs as ARRANGE and the result as ACT; each test writes an ASSERT line before its assertions. No assertion was changed or removed.
- Nothing printed depends on the operating system: `TransferProgressRecorderTests` shows carriage returns and line feeds as `\r` and `\n`, and the platform new line in the redirect-hop status lines as `<newline>`.
- Counts (Assert. / [TestMethod / [DataRow(), before = after: TransferDiagnosticLinesTests 13/9/0, TransferProgressRecorderTests 31/24/2, TransferProxySelectionPreProxyTests 17/13/10, TransferTimersTests 7/4/0, UrlEffectiveTests 6/6/18, UrlTransferTests 28/7/0, WarningLineWrapperTests 12/11/2, WindowsOutputFileNameSanitizerTests 1/1/11, WriteGateStreamTests 16/7/0, WriteGateTests 9/5/0.
- The filtered run printed 119 END lines for 119 tests, none with a zero count; no test printed a `SLOW:` line.
- `Curl.Console.UnitTests` fast tests: 2661 passed, 24 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. TransferDiagnosticLinesTests to WriteGateTests write ARRANGE, ACT and ASSERT diagnostics
