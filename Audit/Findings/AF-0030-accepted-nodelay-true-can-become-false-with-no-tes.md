---
id: AF-0030
title: `accepted.NoDelay = true` can become false with no test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/TcpPendingConnection.cs:AcceptedSocketNoDelay:surviving-mutant
task: none
found: 2026-10-03
found-at: d1db9881553d55cd92c0e74bb41561c3dea9ea84
scorecard: 2026-10-03_1233.md
closed:
closed-by:
---
# AF-0030 - `accepted.NoDelay = true` can become false with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86`: `accepted.NoDelay = true` can become false with no test failing. Reported by an auditor flagged unreliable in 2026-10-03_1233.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86`

Mutation seed 0 changed `accepted.NoDelay = true;` to `false`; survived. TCP_NODELAY on an accepted socket changes wire timing and no test pins it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: Mutant at TcpPendingConnection.cs:86 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86 true

## Re-audits

## Log

- 2026-10-03: filed proposed.
