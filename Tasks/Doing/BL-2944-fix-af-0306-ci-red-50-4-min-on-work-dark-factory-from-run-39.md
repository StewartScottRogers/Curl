---
id: BL-2944
title: Fix AF-0306: CI red 50.4 min on work/dark-factory from run 39210403935 (Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwice...)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-21
completed:
---
# BL-2944 — Fix AF-0306: CI red 50.4 min on work/dark-factory from run 39210403935 (Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwice...)

## Goal

The defect the audit office reported as AF-0306 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0306 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0306-ci-red-50-4-min-on-work-dark-factory-from-run-3921.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells[0]: start 2026-10-19T06:50:57Z, end 07:41:20Z, 50.38 min, runId 39210403935, head 1c3d8cc6 ('chore(tasks): requeue BL-2744 ...'). The commit only carried the regression; it did not cause it. Failing test: Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwice..., off Windows. Timeline (MST): red at 23:50:57. BL-2790 filed at 23:53:15 (DarkFactory-20261018-192454.log:71). Claimed by L1 at 23:58:18, Done and pushed at 00:04:20, 13.4 min after red. Run 39212464400 on 91b509f6 also failed. Runs 39213638245 and 39214116664 were cancelled by newer pushes. Green came only with run 39214162783 on 7d6338c7 at 00:41:20. Most of the spell (37 min) was CI on the fix commit, not repair time.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 39210403935
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-19T06:50:57Z end 2026-10-19T07:41:20Z minutes 50.38 runId 39210403935 stillRed False

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
