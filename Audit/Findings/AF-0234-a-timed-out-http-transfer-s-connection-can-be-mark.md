---
id: AF-0234
title: A timed-out HTTP transfer's connection can be marked keep-alive with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-18_1045.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:OutcomeOfTimeout-false:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1348:false
task: none
tasks:
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed: 2026-10-18
closed-how: mechanical
closed-by: 2026-10-18_1045.md
---
# AF-0234 - A timed-out HTTP transfer's connection can be marked keep-alive with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1348`: A timed-out HTTP transfer's connection can be marked keep-alive with no test failing. Reported by an auditor flagged unreliable in 2026-10-16_1623.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1348`

Mutant 'return new HttpAttemptOutcome(timedOut, null, KeepsAlive: false);' -> 'KeepsAlive: true' survived. A connection abandoned mid-response by -m/exit 28 would be pooled, and a later URL in the same run could read the rest of the old response as its own. The timeout tests check the exit-28 message, body and report, but not that the connection is closed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:1348:false -Member OutcomeOfTimeout -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-18 | 2026-10-18_1045.md | reproduces: no | Ran the -Site command: site resolved to line 1347 ('return new HttpAttemptOutcome(timedOut, null, KeepsAlive: false)' -> true), outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-16: filed proposed.
- 2026-10-18: proposed -> closed. Re-audit 2026-10-18_1045.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
