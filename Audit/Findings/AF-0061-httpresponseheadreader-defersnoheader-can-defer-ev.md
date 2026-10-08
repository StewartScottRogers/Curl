---
id: AF-0061
title: HttpResponseHeadReader.DefersNoHeader can defer every header (false -> true) with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs:DefersNoHeader-false:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs:56:false
task: BL-1704
tasks: BL-1704
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed: 2026-10-08
closed-how: mechanical
closed-by: 2026-10-08_0748.md
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

- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | Ran the -Site command: killed at HttpResponseHeadReader.cs:56 (DefersNoHeader '=> false' -> '=> true'). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
- 2026-10-08: accepted -> closed. Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
