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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
