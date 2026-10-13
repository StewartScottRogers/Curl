---
id: AF-0168
title: ReportUploadRewind's '< 400' can become '<= 400' with no test failing: a 400 after a -L upload writes a false 'Need to rewind upload' verbose line
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:ReportUploadRewind-le:surviving-mutant
reproduction: none
task: BL-2103
tasks: BL-2103
found: 2026-10-11
found-at: d8245edb7b661211c6628e9062e9939934939fb2
scorecard: 2026-10-11_1638.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0168 - ReportUploadRewind's '< 400' can become '<= 400' with no test failing: a 400 after a -L upload writes a false 'Need to rewind upload' verbose line

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1473`: ReportUploadRewind's '< 400' can become '<= 400' with no test failing: a 400 after a -L upload writes a false 'Need to rewind upload' verbose line.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1473`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0: mutant HttpProtocolHandler.cs:1473 '&& statusLine.StatusCode is >= 300 and < 400' -> '... and <= 400' survived. Under the mutant, a -L -v request with a non-empty body answered '400' writes '* Need to rewind upload for next request' (and ReportUploadClosed's line) to standard error. Curl 8.21.0 writes these only for 3xx, as the method's own doc comment says (measured, BL-1213 Notes). Those are -v output bytes no test pins at 400.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1473:< -Member ReportUploadRewind -ExcludeBaselineFailures -TimeoutSeconds 120
```

- Expected: outcome killed: a -L upload answered 400 is asserted to write no rewind line
- Actual: outcome survived

## Re-audits

- 2026-10-12 | 2026-10-12_1653.md | reproduces: no | Ran Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1473:< -Member ReportUploadRewind -ExcludeBaselineFailures -TimeoutSeconds 120: the site moved and resolved to line 1491 in ReportUploadRewind, '&& statusLine.StatusCode is >= 300 and < 400' -> '<= 400', outcome killed (7829 ms). The baseline excluded the same 21 failing tests.

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
