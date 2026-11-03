---
id: BL-4767
title: Fix AF-0603: Surviving mutant: the progress meter can stop ticking when only one of its outputs is set without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4767 — Fix AF-0603: Surviving mutant: the progress meter can stop ticking when only one of its outputs is set without a test failing

## Goal

The defect the audit office reported as AF-0603 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0603 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0603-surviving-mutant-the-progress-meter-can-stop-ticki.md`.

Location: `Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:174`

Location: `Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:174`

writeLive is not null || progressBar is not null -> && survived (sample and targeted rerun, 23 baseline-failing tests left out): with only the live meter or only the bar, no timer is created, so the meter is not redrawn each second during a transfer.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferProgressRecorder.cs:174:|| -Member TransferProgressRecorder -ExcludeBaselineFailures -TimeoutSeconds 600
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
