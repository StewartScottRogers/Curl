---
id: BL-2102
title: Fix AF-0167: HttpContentRange.HonoursResume's 'statusCode < 300' can become '<= 300' with no test failing: a 300 with Content-Range '*' is taken as a resume
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-12
completed:
---
# BL-2102 — Fix AF-0167: HttpContentRange.HonoursResume's 'statusCode < 300' can become '<= 300' with no test failing: a 300 with Content-Range '*' is taken as a resume

## Goal

The defect the audit office reported as AF-0167 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0167 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0167-httpcontentrange-honoursresume-s-statuscode-300-ca.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpContentRange.cs:39`

Location: `Curl.Protocol.Http.UnitLibrary/HttpContentRange.cs:39`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0: mutant HttpContentRange.cs:39 'return statusCode < 300;' -> 'return statusCode <= 300;' survived. HonoursResume decides DecideResume (HttpDownloadConditions.cs:176): true delivers the body; false ends in NothingLeftToResume or throws HttpTransferException(CurlExitCode.RangeError, ResumeNotSupported), exit 33. Under the mutant, a -C transfer answered '300' with 'Content-Range: bytes */N' delivers the body as a resume instead of failing with exit 33. HttpContentRangeTests.HonoursResume_ReadsTheFirstNumber pins 200 and 404 for the asterisk form, but not the 2xx/3xx boundary.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpContentRange.cs:39:< -Member HonoursResume -ExcludeBaselineFailures -TimeoutSeconds 120
```

- Expected: outcome killed: a test with status 300 and 'Content-Range: bytes */N' expects no resume
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing.
