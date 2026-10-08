---
id: BL-1688
title: Fix AF-0069: Requirements FR-007 cites test RunAsync_RangeThatNamesNoRange_ReturnsExit33WithoutDispatching, which does not exist
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1688 — Fix AF-0069: Requirements FR-007 cites test RunAsync_RangeThatNamesNoRange_ReturnsExit33WithoutDispatching, which does not exist

## Goal

The defect the audit office reported as AF-0069 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0069 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0069-requirements-fr-007-cites-test-runasync-rangethatn.md`.

Location: `Documentation/Product/Requirements.md:40`

Location: `Documentation/Product/Requirements.md:40`

FR-007 says the range hand-off is 'shown by `CurlCommandRunnerTransferOptionTests.RunAsync_Range_WritesOnlyThoseBytes` and `RunAsync_RangeThatNamesNoRange_ReturnsExit33WithoutDispatching`'. The second test exists nowhere in the solution. The tests that do exist describe different behaviour: CurlCommandRunnerRangeTextHandOffTests.RunAsync_SilentVerboseFileRangeThatNamesNoRange_ShutsDownTheConnectionAndExits33 (a connection is made and shut down, not 'without dispatching'), and CurlCommandRunnerTransferOptionTests.RunAsync_HttpRangeThatNamesNoRange_DispatchesTheTextAsTyped. An agent checking the requirement's evidence finds nothing, and the 'without dispatching' claim is no longer what the runner does.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Get-ChildItem -Path Curl.Console.UnitTests -Recurse -Filter *.cs | Select-String -SimpleMatch 'RunAsync_RangeThatNamesNoRange_ReturnsExit33WithoutDispatching').Count
```

- Expected: 1 or more: the cited test exists
- Actual: 0 (Requirements.md:40 cites it)

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The finding's defect is the requirement citing evidence that does not exist, so the fix is the
  citation, not a test renamed to match it: a test named `..._ReturnsExit33WithoutDispatching`
  would itself be untrue, because since BL-1322/BL-1396 the runner hands range text naming no
  range to every handler. FR-007 now cites only tests that exist and say what the runner does:
  `CurlCommandRunnerTransferOptionTests.RunAsync_Range_WritesOnlyThoseBytes`,
  `CurlCommandRunnerRangeTextHandOffTests.RunAsync_SilentVerboseFileRangeThatNamesNoRange_ShutsDownTheConnectionAndExits33`
  and `CurlCommandRunnerTransferOptionTests.RunAsync_HttpRangeThatNamesNoRange_DispatchesTheTextAsTyped`.
  Each was checked with a `grep` of `Curl.Console.UnitTests`: one match apiece. The literal
  reproduction command searches for the old, nonexistent name and so still prints 0; what it
  verifies ("the cited test exists") now holds for every test FR-007 cites, and the old name
  appears nowhere outside `Audit/` and `Tasks/`.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. FR-007 now cites only existing tests that describe the range hand-off as the runner does it
