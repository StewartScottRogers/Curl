---
id: AF-0606
title: Surviving mutant: SslStreamConnection can send close_notify more than once without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/SslStreamConnection.cs:SendCloseNotifyAsync-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/SslStreamConnection.cs:202:true
task: BL-4770
tasks: BL-4770
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1615.md
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

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the -Site command: SslStreamConnection.cs:202 true resolved to line 202, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
