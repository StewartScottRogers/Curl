---
id: AF-0233
title: After giving up on refused-stream retries the connection can be kept alive with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:StreamRefusedOutcome-false:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1975:false
task: BL-2570
tasks: BL-2570
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0233 - After giving up on refused-stream retries the connection can be kept alive with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1975`: After giving up on refused-stream retries the connection can be kept alive with no test failing. Reported by an auditor flagged unreliable in 2026-10-16_1623.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1975`

Mutant 'new HttpAttemptOutcome(refused with { ErrorMessage = ConnectionDiedGivingUp(...) }, null, KeepsAlive: false)' -> 'KeepsAlive: true' survived. A connection the peer kept refusing would go back to the pool, and the next URL of the same run could reuse it. No test checks the pool state after the 'Connection died, tried N times before giving up' outcome.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1975:false -Member StreamRefusedOutcome -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-18 | 2026-10-18_1045.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: site resolved to line 1993 ('KeepsAlive: false' -> 'KeepsAlive: true' in StreamRefusedOutcome), outcome survived, excludedTests empty. Runner's targeted mutation rerun on the clean audited commit: survived.

## Log

- 2026-10-16: filed proposed.
- 2026-10-16: proposed -> accepted.
