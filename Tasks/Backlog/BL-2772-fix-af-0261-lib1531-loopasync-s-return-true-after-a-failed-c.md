---
id: BL-2772
title: Fix AF-0261: Lib1531 LoopAsync's return true after a failed curl_multi_fdset can become false with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-10-18
completed:
---
# BL-2772 — Fix AF-0261: Lib1531 LoopAsync's return true after a failed curl_multi_fdset can become false with no test failing

## Goal

The defect the audit office reported as AF-0261 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0261 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0261-lib1531-loopasync-s-return-true-after-a-failed-cur.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1531.cs:102`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1531.cs:102`

LibTest sample, seed 0: survived. Original `return true;` after writing 'curl_multi_fdset() failed, code N.', mutated `return false;`. This flips the loop's continue-or-stop decision on an fdset failure, which changes how the port ends. No test reaches that branch with a check on what follows.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1531.cs:102:true -Member LoopAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
