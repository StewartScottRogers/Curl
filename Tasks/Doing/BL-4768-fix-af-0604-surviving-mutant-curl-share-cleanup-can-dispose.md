---
id: BL-4768
title: Fix AF-0604: Surviving mutant: curl_share_cleanup can dispose the shared connections when it refuses and keep them when it succeeds without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4768 — Fix AF-0604: Surviving mutant: curl_share_cleanup can dispose the shared connections when it refuses and keep them when it succeeds without a test failing

## Goal

The defect the audit office reported as AF-0604 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0604 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0604-surviving-mutant-curl-share-cleanup-can-dispose-th.md`.

Location: `Curl.Engine.UnitLibrary/ManagedShareHandle.cs:102`

Location: `Curl.Engine.UnitLibrary/ManagedShareHandle.cs:102`

if (code == CurlShareCode.Ok) -> != survived (sample and targeted rerun): a cleanup refused as InUse then disposes the pool the attached transfers still use, and a successful one leaks it.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedShareHandle.cs:102:== -Member Cleanup -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
