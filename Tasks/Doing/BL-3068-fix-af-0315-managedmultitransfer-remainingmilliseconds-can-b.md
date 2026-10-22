---
id: BL-3068
title: Fix AF-0315: ManagedMultiTransfer.RemainingMilliseconds' || can become && with no test failing, so curl_multi_timeout answers 0 instead of -1 for running transfers with no timeout
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-22
completed:
---
# BL-3068 — Fix AF-0315: ManagedMultiTransfer.RemainingMilliseconds' || can become && with no test failing, so curl_multi_timeout answers 0 instead of -1 for running transfers with no timeout

## Goal

The defect the audit office reported as AF-0315 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0315 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0315-managedmultitransfer-remainingmilliseconds-can-bec.md`.

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:831`

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:831`

Sampled mutant (seed 0) survived: 'if (entry.Done || entry.Perform is null || limit <= TimeSpan.Zero) return -1;' with the first || turned into &&. For a running transfer with no CURLOPT_TIMEOUT (limit 0), the mutant skips the early return and computes Math.Max(0, ceil(0 - elapsed)) = 0. GetTimeout (line 380) then takes that 0 as its minimum, so it answers 0 instead of -1. WaitAsync caps its wait at GetTimeout, so a multi loop would spin. curl_multi_timeout's answer is visible to every Libcurl and Curl.Api multi user.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:831:|| -Member RemainingMilliseconds -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
