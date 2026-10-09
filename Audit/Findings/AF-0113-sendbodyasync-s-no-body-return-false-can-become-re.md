---
id: AF-0113
title: SendBodyAsync's no-body 'return false' can become 'return true' with no test failing
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-09_0647.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:SendBodyAsync-false:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433:false
task: BL-1863
tasks: BL-1863
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed: 2026-10-09
closed-how: mechanical
closed-by: 2026-10-09_0647.md
---
# AF-0113 - SendBodyAsync's no-body 'return false' can become 'return true' with no test failing

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433`: SendBodyAsync's no-body 'return false' can become 'return true' with no test failing. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433`

Mutant 'return false;' -> 'return true;' at line 2433 survived (sampled run, seed 0). The return value is bodyLeftUnsent (line 1129): true means a final status arrived during the 100-continue wait and the body was left unsent. For a request with no body, the mutant makes ReportRequestSent skip nothing visible but makes RetryOfAsync/RetriesWithoutExpect (line 2148) treat a keep-alive 417 as 'body stopped': it reports 'Got 417 while waiting for a 100' and retries with plan.Framing.Body! being null. A bodyless request answered by 417 would then be resent or fail instead of ending with the 417, changing request bytes and exit code. No test sends a bodyless request that gets a 417.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433:false -Member SendBodyAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Ran the -Site reproduction: survived Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433 false [SendBodyAsync] 'return false;' -> 'return true;', excludedTests empty. The seed-0 sample of the library hit the same site, and it survived there too.
- 2026-10-09 | 2026-10-09_0647.md | reproduces: no | Ran the -Site reproduction: 'killed    Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:2433 false' (return false -> return true, member SendBodyAsync, outcome killed). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0647.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
