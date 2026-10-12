---
id: BL-2103
title: Fix AF-0168: ReportUploadRewind's '< 400' can become '<= 400' with no test failing: a 400 after a -L upload writes a false 'Need to rewind upload' verbose line
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-12
completed:
---
# BL-2103 — Fix AF-0168: ReportUploadRewind's '< 400' can become '<= 400' with no test failing: a 400 after a -L upload writes a false 'Need to rewind upload' verbose line

## Goal

The defect the audit office reported as AF-0168 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0168 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0168-reportuploadrewind-s-400-can-become-400-with-no-te.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1473`

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1473`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0: mutant HttpProtocolHandler.cs:1473 '&& statusLine.StatusCode is >= 300 and < 400' -> '... and <= 400' survived. Under the mutant, a -L -v request with a non-empty body answered '400' writes '* Need to rewind upload for next request' (and ReportUploadClosed's line) to standard error. Curl 8.21.0 writes these only for 3xx, as the method's own doc comment says (measured, BL-1213 Notes). Those are -v output bytes no test pins at 400.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1473:< -Member ReportUploadRewind -ExcludeBaselineFailures -TimeoutSeconds 120
```

- Expected: outcome killed: a -L upload answered 400 is asserted to write no rewind line
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-12: Created.
