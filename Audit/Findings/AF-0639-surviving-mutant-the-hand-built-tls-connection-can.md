---
id: AF-0639
title: Surviving mutant: the hand-built TLS connection can forget it already sent close_notify and send it again, and no test fails
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:SendCloseNotifyAsync-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:152:true
task: BL-4818
tasks: BL-4818
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0639 - Surviving mutant: the hand-built TLS connection can forget it already sent close_notify and send it again, and no test fails

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:152`: Surviving mutant: the hand-built TLS connection can forget it already sent close_notify and send it again, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:152`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 221035: survived, '_tlsEnded = true;' -> '_tlsEnded = false;' in SendCloseNotifyAsync. The guard at line 147 then lets a second SendCloseNotifyAsync (dispose after an explicit shutdown) run ShutdownAsync again, putting a second close_notify alert on the wire. HandBuiltTlsConnectionCloseNotifyTests checks a cleared connection but never a second shutdown.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltTlsConnection.cs:152:true -Member SendCloseNotifyAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
