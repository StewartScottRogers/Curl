---
id: AF-0844
title: Surviving mutant: an upload's progress bar is redrawn as a download at the end
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/ProgressBarRecorder.cs:ReportUploaded-true:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ProgressBarRecorder.cs:156:true
task: none
tasks:
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0844 - Surviving mutant: an upload's progress bar is redrawn as a download at the end

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ProgressBarRecorder.cs:156`: Surviving mutant: an upload's progress bar is redrawn as a download at the end.

## Evidence

Location: `Curl.Engine.UnitLibrary/ProgressBarRecorder.cs:156`

bytesReported = true; -> false in ReportUploaded survived. Finish(succeeded, bytesTransferred) then thinks no bytes were reported and calls ReportDownloaded(bytesTransferred, bytesTransferred), adding a download total to an upload-only transfer's -# bar. That changes the stderr progress bytes. ReportUploaded_HalfTheUpload_DrawsAHalfBar never calls Finish.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ProgressBarRecorder.cs:156:true -Member ReportUploaded -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-17: filed proposed.
