---
id: AF-0606
title: Surviving mutant: SslStreamConnection can send close_notify more than once without a test failing
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/SslStreamConnection.cs:SendCloseNotifyAsync-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/SslStreamConnection.cs:202:true
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0606 - Surviving mutant: SslStreamConnection can send close_notify more than once without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/SslStreamConnection.cs:202`: Surviving mutant: SslStreamConnection can send close_notify more than once without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/SslStreamConnection.cs:202`

_tlsEnded = true -> false survived (sample and targeted rerun, with the 102 baseline-failing tests left out): a later close or clear then shuts TLS down again, a second alert on the wire.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/SslStreamConnection.cs:202:true -Member SendCloseNotifyAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
