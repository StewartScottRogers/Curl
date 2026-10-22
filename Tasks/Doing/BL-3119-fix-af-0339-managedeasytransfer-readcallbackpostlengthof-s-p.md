---
id: BL-3119
title: Fix AF-0339: ManagedEasyTransfer.ReadCallbackPostLengthOf's PostFieldSize < 0 can become <= 0 with no test failing, so a read-callback post with CURLOPT_POSTFIELDSIZE 0 goes chunked
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-22
completed:
---
# BL-3119 — Fix AF-0339: ManagedEasyTransfer.ReadCallbackPostLengthOf's PostFieldSize < 0 can become <= 0 with no test failing, so a read-callback post with CURLOPT_POSTFIELDSIZE 0 goes chunked

## Goal

The defect the audit office reported as AF-0339 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0339 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0339-managedeasytransfer-readcallbackpostlengthof-s-pos.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:916`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:916`

Sampled mutant (seed 0) survived: 'http.PostFieldSize < 0 || AsksForChunked(...) ? null : http.PostFieldSize' became '<= 0'. A CURLOPT_POST with a read callback and CURLOPT_POSTFIELDSIZE 0 should send Content-Length: 0, as libcurl 8.22.0 does. With the mutant it sends Transfer-Encoding: chunked. The request bytes change and no test noticed. The existing test PerformAsync_PostWithNoPostFieldsAPostFieldSizeOfZeroAndNoReadCallback_... covers size 0 only without a read callback.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:916:< -Member ReadCallbackPostLengthOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
