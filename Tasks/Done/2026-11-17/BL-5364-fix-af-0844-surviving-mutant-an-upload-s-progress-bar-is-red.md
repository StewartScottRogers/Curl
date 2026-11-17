---
id: BL-5364
title: Fix AF-0844: Surviving mutant: an upload's progress bar is redrawn as a download at the end
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ProgressBarRecorder.cs, Curl.Engine.UnitTests/ProgressBarRecorderTests.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5364 — Fix AF-0844: Surviving mutant: an upload's progress bar is redrawn as a download at the end

## Goal

The defect the audit office reported as AF-0844 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0844 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ProgressBarRecorder.cs:156`

Location: `Curl.Engine.UnitLibrary/ProgressBarRecorder.cs:156`

bytesReported = true; -> false in ReportUploaded survived. Finish(succeeded, bytesTransferred) then thinks no bytes were reported and calls ReportDownloaded(bytesTransferred, bytesTransferred), adding a download total to an upload-only transfer's -# bar. That changes the stderr progress bytes. ReportUploaded_HalfTheUpload_DrawsAHalfBar never calls Finish.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ProgressBarRecorder.cs line 156` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ProgressBarRecorder.cs line 156`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by Finish_SucceededAfterAnUploadReport_DrawsNothingMore (new; failed with the mutant, passes reverted). Test file added to touches. Build clean, Engine fast tests green.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 91036ec4 9742157d.
- 2026-11-17: Doing -> Done. Mutant killed by new Finish test
