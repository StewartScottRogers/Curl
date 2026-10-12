---
id: AF-0167
title: HttpContentRange.HonoursResume's 'statusCode < 300' can become '<= 300' with no test failing: a 300 with Content-Range '*' is taken as a resume
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Protocol.Http.UnitLibrary/HttpContentRange.cs:HonoursResume-le:surviving-mutant
reproduction: none
task: none
tasks:
found: 2026-10-11
found-at: d8245edb7b661211c6628e9062e9939934939fb2
scorecard: 2026-10-11_1638.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0167 - HttpContentRange.HonoursResume's 'statusCode < 300' can become '<= 300' with no test failing: a 300 with Content-Range '*' is taken as a resume

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpContentRange.cs:39`: HttpContentRange.HonoursResume's 'statusCode < 300' can become '<= 300' with no test failing: a 300 with Content-Range '*' is taken as a resume.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpContentRange.cs:39`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0: mutant HttpContentRange.cs:39 'return statusCode < 300;' -> 'return statusCode <= 300;' survived. HonoursResume decides DecideResume (HttpDownloadConditions.cs:176): true delivers the body; false ends in NothingLeftToResume or throws HttpTransferException(CurlExitCode.RangeError, ResumeNotSupported), exit 33. Under the mutant, a -C transfer answered '300' with 'Content-Range: bytes */N' delivers the body as a resume instead of failing with exit 33. HttpContentRangeTests.HonoursResume_ReadsTheFirstNumber pins 200 and 404 for the asterisk form, but not the 2xx/3xx boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpContentRange.cs:39:< -Member HonoursResume -ExcludeBaselineFailures -TimeoutSeconds 120
```

- Expected: outcome killed: a test with status 300 and 'Content-Range: bytes */N' expects no resume
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-11: filed proposed.
