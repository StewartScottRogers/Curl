---
id: BL-2569
title: Fix AF-0232: ReportUploadRewind's 3xx range can drop 300 with no test failing, losing curl's 'Need to rewind upload' -v line
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-17
completed:
---
# BL-2569 — Fix AF-0232: ReportUploadRewind's 3xx range can drop 300 with no test failing, losing curl's 'Need to rewind upload' -v line

## Goal

The defect the audit office reported as AF-0232 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0232 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0232-reportuploadrewind-s-3xx-range-can-drop-300-with-n.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1584`

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1584`

Mutant '&& statusLine.StatusCode is >= 300 and < 400' -> '> 300 and < 400' survived (seed 0, sample of 40). With -L and a non-empty body, a 300 Multiple Choices reply would no longer write '* Need to rewind upload for next request' (nor the close line after it) to the verbose output, which is stderr output bytes. The upload-rewind tests use 301/302/303/307/308 only.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1584:>= -Member ReportUploadRewind -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-17: Created.
