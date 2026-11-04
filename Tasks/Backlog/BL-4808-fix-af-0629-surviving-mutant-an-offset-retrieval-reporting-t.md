---
id: BL-4808
title: Fix AF-0629: Surviving mutant: an offset retrieval reporting the whole upload received can fail with exit 8, and no test fails
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4808 — Fix AF-0629: Surviving mutant: an offset retrieval reporting the whole upload received can fail with exit 8, and no test fails

## Goal

The defect the audit office reported as AF-0629 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0629 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0629-surviving-mutant-an-offset-retrieval-reporting-the.md`.

Location: `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs:47`

Location: `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs:47`

Invoke-MutationTest -Library Curl.Extension.ResumableUpload.UnitLibrary -MaxMutants 25 -Seed 221035: survived, 'offset <= length' -> 'offset < length' in OffsetIn. When the server's HEAD says every byte arrived (Upload-Offset equal to the body length), the mutant throws ResumableUploadTransferException(CurlExitCode.WeirdServerReply, ...) instead of sending the final PATCH, so a resumed upload ends with exit 8. The tests cover offset 3 of 5 and a missing offset, never offset == length.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs:47:<= -Member OffsetIn -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
