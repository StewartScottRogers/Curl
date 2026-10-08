---
id: AF-0061
title: HttpResponseHeadReader.DefersNoHeader can defer every header (false -> true) with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs:DefersNoHeader-false:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs:56:false
task: none
tasks:
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0061 - HttpResponseHeadReader.DefersNoHeader can defer every header (false -> true) with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs:56`: HttpResponseHeadReader.DefersNoHeader can defer every header (false -> true) with no test failing.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs:56`

Mutant: DefersNoHeader = static (_, _) => false; -> => true. It survived the sampled run (seed 0). DefersNoHeader is the default DefersFrom (line 193, documented 'By default no header is deferred'). DefersFrom decides, at line 622, whether a header and every header after it are kept from HeaderReceived and the -v report until they are released. Every head reader built without DefersFrom (only HttpProtocolHandler.cs:1052 sets it) would then defer every acted-on header. No test checks the default reader's header delivery order against that.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs:56:false -Member DefersNoHeader -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
