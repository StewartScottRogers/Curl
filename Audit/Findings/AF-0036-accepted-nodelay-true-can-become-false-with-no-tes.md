---
id: AF-0036
title: `accepted.NoDelay = true` can become false with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/TcpPendingConnection.cs:accepted.NoDelay:surviving-mutant
task: none
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
closed:
closed-by:
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
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: The mutant at TcpPendingConnection.cs:86 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86 true

## Re-audits

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
