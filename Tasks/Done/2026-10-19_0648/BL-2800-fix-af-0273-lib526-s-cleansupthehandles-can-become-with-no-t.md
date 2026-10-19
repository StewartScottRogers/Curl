---
id: BL-2800
title: Fix AF-0273: Lib526's CleansUpTheHandles || can become && with no test failing, so cases 526, 528 and 532 skip their clean-up
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2800 — Fix AF-0273: Lib526's CleansUpTheHandles || can become && with no test failing, so cases 526, 528 and 532 skip their clean-up

## Goal

The defect the audit office reported as AF-0273 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0273 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0273-lib526-s-cleansupthehandles-can-become-with-no-tes.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`

Mutant testNumber is 526 or 528 or 532 || (testNumber == 527 && steps.Result != CurlCode.Ok) -> && survived (sampled run, seed 0). With && the expression is always false, so no case cleans up its handles the way upstream lib526.c does for its case numbers. This is a decision the port makes, and no test pins it.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147:|| -Member CleansUpTheHandles -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Conformance.LibTest.UnitTests` to `touches`: the fix is a test that kills the mutant (no production change is needed), and no task in Doing on origin/work/dark-factory names that project.
- Decision: the fake engine counts disposed easy transfers, and a test pins that 526, 528 and 532 clean up all four handles while 527 (success) cleans each up as it advances and 999 cleans up none, as upstream lib526.c does.
- Verified by hand (a lane may not run `Audit/Tools`): with `||` changed to `&&` on Lib526.cs:147, the new test fails for 526, 528 and 532; restored, it passes.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-19: Doing -> Done. Lib526's clean-up is pinned: a fake engine counts disposed easy handles and the mutant on line 147 now fails three cases
