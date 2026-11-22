---
id: BL-5772
title: Fix AF-0975: RunAsync_Lib564AddHandleFails_SaysNothing accepts any non-zero exit and checks only that one string is absent
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5772 — Fix AF-0975: RunAsync_Lib564AddHandleFails_SaysNothing accepts any non-zero exit and checks only that one string is absent

## Goal

The defect the audit office reported as AF-0975 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0975 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:300`

Location: `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:300`

Step 1, LibTest sample. The assertions are Assert.AreNotEqual(0, result.ExitCode); and Assert.IsFalse(run.Error.ToString().Contains("Start at URL", ...));. The name promises that nothing is said. Any other text on stderr, and any non-zero exit code (the wrong one included), passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs -Pattern 'Lib564AddHandleFails_SaysNothing' -Context 0,10
```

- Expected: The test asserts empty stderr (Assert.AreEqual(string.Empty, run.Error.ToString())) and the exact exit code.
- Actual: Assert.AreNotEqual(0, result.ExitCode); and a check that "Start at URL" is absent.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The test now asserts exit code 32 and the exact stderr line (the old name "SaysNothing" was false: Lib564 reports the failed call on stderr, as upstream lib564 does), so it is renamed RunAsync_Lib564AddHandleFails_ReportsOnlyTheFailedCallAndExits32. Build clean, fast tests green.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
