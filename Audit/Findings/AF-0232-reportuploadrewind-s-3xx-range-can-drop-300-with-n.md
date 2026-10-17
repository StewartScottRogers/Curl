---
id: AF-0232
title: ReportUploadRewind's 3xx range can drop 300 with no test failing, losing curl's 'Need to rewind upload' -v line
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:ReportUploadRewind-ge:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1584:>=
task: none
tasks:
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0232 - ReportUploadRewind's 3xx range can drop 300 with no test failing, losing curl's 'Need to rewind upload' -v line

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1584`: ReportUploadRewind's 3xx range can drop 300 with no test failing, losing curl's 'Need to rewind upload' -v line. Reported by an auditor flagged unreliable in 2026-10-16_1623.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1584`

Mutant '&& statusLine.StatusCode is >= 300 and < 400' -> '> 300 and < 400' survived (seed 0, sample of 40). With -L and a non-empty body, a 300 Multiple Choices reply would no longer write '* Need to rewind upload for next request' (nor the close line after it) to the verbose output, which is stderr output bytes. The upload-rewind tests use 301/302/303/307/308 only.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1584:>= -Member ReportUploadRewind -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-16: filed proposed.
