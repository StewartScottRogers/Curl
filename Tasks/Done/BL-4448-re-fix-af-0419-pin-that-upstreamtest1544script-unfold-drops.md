---
id: BL-4448
title: Re-fix AF-0419: pin that UpstreamTest1544Script.Unfold drops comment lines and keeps option lines whether or not a fold is pending
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs, Curl.Conformance.UnitTests/UpstreamTest1544ScriptTests.cs]
requirement: none
created: 2026-11-01
completed: 2026-11-01
---
# BL-4448 — Re-fix AF-0419: pin that UpstreamTest1544Script.Unfold drops comment lines and keeps option lines whether or not a fold is pending

## Goal

`UpstreamTest1544ScriptTests` fails if `UpstreamTest1544Script.Unfold` lets a leading space (or any other prefix from the fold handling) change which lines count as comments or which options the script finds.

## Context

Audit finding AF-0419 (accepted 2026-10-24, quality auditor, Medium): `Unfold`'s old `folded.Length > 0 ? folded + " " + line : line` could become `>= 0` with no test failing, so every unfolded line gained a leading space; comment lines were then no longer dropped and the option patterns saw a shifted line. BL-3468 rewrote `Unfold` (`Curl.Conformance.UnitLibrary/UpstreamTest1544Script.cs`, about line 116) to always do `line = folded + " " + line;` and rely on `WithoutComments` trimming. The mutation site is gone, so 8 re-audits in a row reported "site missing" and the finding stays open. Stewart chose a re-fix on 2026-11-01. What is missing is a test of the behaviour the finding is about, so a re-audit can judge it by reading the tests.

## Acceptance criteria

- [x] A test in `Curl.Conformance.UnitTests/UpstreamTest1544ScriptTests.cs` feeds the script a header with a `#` comment line and an option line, with no fold pending, and asserts the comment line contributes nothing and the option is found exactly as written.
- [x] A second test does the same right after a folded (continued) line, and asserts the joined line yields the expected option and the following comment line is still dropped.
- [x] Changing `WithoutComments` to stop trimming its input (a temporary local edit, reverted) makes at least one of the new tests fail; Notes record the failing test name.
- [x] `dotnet build` is clean, the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green, and the quality gates hold for `Curl.Conformance.UnitLibrary`.

## Notes

- Added `Check_HashLineAndOptionLineWithNoFoldPending_DropsTheHashLineAndFindsTheOptionAsWritten` and `Check_HashLineRightAfterAContinuedLine_JoinsTheOptionAndStillDropsTheHashLine`. The `#` line holds a `CURLOPTTYPE_STRINGPOINT` declaration, so a `#` line that survives shows up as `CURLOPT_IN_HASH_LINE is not translated`.
- Mutation check: a temporary edit making `WithoutComments` return `(line, false)` with no trim (since reverted) failed both new tests and `Check_CommentsContinuationsAndFolds_ReadsTheStringOptionsAsPerlDoes`.
- The production code is unchanged (only tests were added), so `Curl.Conformance.UnitLibrary`'s coverage and complexity stay as they were and no Measure-CodeQuality run was needed. The build is clean and the fast tests are green (114 test projects passed).

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: 09013feb 1187be88 73f7312e 861d05b4.
- 2026-11-01: Doing -> Done. Two tests pin that Unfold drops # lines and finds options as written, with and without a pending fold; no-trim mutation fails them
