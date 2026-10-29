---
id: BL-4178
title: Fix AF-0455: ParallelProgressMeter.AddTotals's UploadTotalAdded = true can become false with no test failing, so the -Z meter adds an upload's size again on every update
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-29
completed:
---
# BL-4178 — Fix AF-0455: ParallelProgressMeter.AddTotals's UploadTotalAdded = true can become false with no test failing, so the -Z meter adds an upload's size again on every update

## Goal

The defect the audit office reported as AF-0455 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0455 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0455-parallelprogressmeter-addtotals-s-uploadtotaladded.md`.

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:266`

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:266`

Lines 263-267: `if (transfer.UploadTotal != 0 && !transfer.UploadTotalAdded) { uploadTotal += transfer.UploadTotal; transfer.UploadTotalAdded = true; }`. The mutation run (seed 0) changed `true` to `false` and every Curl.Engine.UnitTests test passed. The flag is what makes each upload's size count once (the doc comment says `each once, as curl adds dltotal and ultotal once they are known`). Without it, uploadTotal grows on every progress update, so the parallel progress meter's upload total and percentage printed to stderr are wrong. The download branch two lines above is guarded the same way, so only the upload side is untested.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:266:true -Member AddTotals -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a parallel meter test with an upload of known size updated twice sees its total counted once.
- Actual: outcome survived: every Curl.Engine.UnitTests test passed with the mutant.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
