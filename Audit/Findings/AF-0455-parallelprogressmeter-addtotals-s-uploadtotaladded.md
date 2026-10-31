---
id: AF-0455
title: ParallelProgressMeter.AddTotals's UploadTotalAdded = true can become false with no test failing, so the -Z meter adds an upload's size again on every update
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:AddTotals-true:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:266:true
task: BL-4178
tasks: BL-4178
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0455 - ParallelProgressMeter.AddTotals's UploadTotalAdded = true can become false with no test failing, so the -Z meter adds an upload's size again on every update

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:266`: ParallelProgressMeter.AddTotals's UploadTotalAdded = true can become false with no test failing, so the -Z meter adds an upload's size again on every update. Reported by an auditor flagged unreliable in 2026-10-29_0606.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:266`

Lines 263-267: `if (transfer.UploadTotal != 0 && !transfer.UploadTotalAdded) { uploadTotal += transfer.UploadTotal; transfer.UploadTotalAdded = true; }`. The mutation run (seed 0) changed `true` to `false` and every Curl.Engine.UnitTests test passed. The flag is what makes each upload's size count once (the doc comment says `each once, as curl adds dltotal and ultotal once they are known`). Without it, uploadTotal grows on every progress update, so the parallel progress meter's upload total and percentage printed to stderr are wrong. The download branch two lines above is guarded the same way, so only the upload side is untested.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ParallelProgressMeter.cs:266:true -Member AddTotals -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a parallel meter test with an upload of known size updated twice sees its total counted once.
- Actual: outcome survived: every Curl.Engine.UnitTests test passed with the mutant.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
