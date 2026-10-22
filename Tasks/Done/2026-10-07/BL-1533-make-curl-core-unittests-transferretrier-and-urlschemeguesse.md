---
id: BL-1533
title: Make Curl.Core.UnitTests' TransferRetrier and UrlSchemeGuesser tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1533 — Make Curl.Core.UnitTests' TransferRetrier and UrlSchemeGuesser tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Core.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `TransferRetrierTests.cs` (54 tests), `TransferRetrierDiagnosticLogTests.cs`, `TransferRetryWarningTests.cs`, `UrlSchemeGuesserTests.cs` (77 test methods, counted 2026-10-07).

## Context

- Split from BL-1463 (one per range of files, as its Notes direct); BL-1463 keeps the whole-project checks and depends on this task. Read BL-1463's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; add no prefix of your own; output only; `FakeTimeProvider` advances are `ARRANGE` lines; nothing printed may depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Core.UnitTests --filter "FullyQualifiedName~TransferRetrier|FullyQualifiedName~TransferRetryWarningTests|FullyQualifiedName~UrlSchemeGuesserTests" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean and `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Core.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts of `Assert.` / `[TestMethod` / `[DataRow(` matching lines, before -> after (unchanged): `TransferRetrierTests.cs` 103/54/24 -> 103/54/24; `TransferRetrierDiagnosticLogTests.cs` 8/6/0 -> 8/6/0; `TransferRetryWarningTests.cs` 5/5/13 -> 5/5/13; `UrlSchemeGuesserTests.cs` 12/12/39 -> 12/12/39.
- The filtered detailed run printed 135 `END` lines for 135 tests, none with a zero arrange, act or assert count. Fast run of the project: 1441 passed, 6 skipped, 0 failed.
- No test printed a `SLOW:` line (each ran in a few milliseconds), so there is no `PHASE` breakdown and no follow-up task. None has phases worth timing, so none writes `PHASE` lines.
- Approach: the `Retry` helpers in `TransferRetrierTests` became instance methods that write the policy, URL, per-attempt clock advance and scripted attempts as `ARRANGE` lines and the result, attempt count, waits, warnings and abandon warnings as `ACT` lines; an `Expect` helper writes an `ASSERT` line (collections joined) before each original assertion, which is unchanged. `UrlSchemeGuesserTests`, `TransferRetryWarningTests` and `TransferRetrierDiagnosticLogTests` write theirs through small helpers that return the value the unchanged `Assert` line checks.
- `Measure-CodeQuality.ps1` was not run: only a test project changed, and the quality gates apply to `*.UnitLibrary` and `Curl.Console`.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the four files writes ARRANGE, ACT and ASSERT diagnostics; build clean, fast tests green
