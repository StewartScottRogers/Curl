---
id: AF-0036
title: `accepted.NoDelay = true` can become false with no test failing
auditor: quality
severity: Medium
status: closed
reason: Duplicate of AF-0030: the same surviving mutant (TcpPendingConnection.cs AcceptStreamConnectionAsync, true -> false), whose mechanical key both now carry (ADR-0422).
key: quality:Curl.Networking.UnitLibrary/TcpPendingConnection.cs:AcceptStreamConnectionAsync-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86:true
task: BL-1379
tasks: BL-1379
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
duplicate-of: AF-0030
closed: 2026-10-07
closed-how: duplicate
closed-by: session
---
# AF-0036 - `accepted.NoDelay = true` can become false with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86`: `accepted.NoDelay = true` can become false with no test failing. Reported by an auditor flagged unreliable in 2026-10-03_1459.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86`

Mutant `accepted.NoDelay = false` survived at seed 0. No test checks that the accepted socket has Nagle disabled, so the socket option can regress silently.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86:true -Member AcceptStreamConnectionAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at TcpPendingConnection.cs:86 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86 true

## Re-audits

- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | Same site as AF-0030: TcpPendingConnection.cs:77 'accepted.NoDelay = true' -> 'false', applied by hand with the ECH failures filtered out. Killed by TurnOffNagle_SetsNoDelayOnTheAcceptedSocket.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
- 2026-10-07: accepted -> closed. Duplicate of AF-0030: the same surviving mutant (TcpPendingConnection.cs AcceptStreamConnectionAsync, true -> false), whose mechanical key both now carry (ADR-0422).
