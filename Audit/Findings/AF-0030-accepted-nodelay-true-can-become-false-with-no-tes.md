---
id: AF-0030
title: `accepted.NoDelay = true` can become false with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/TcpPendingConnection.cs:AcceptStreamConnectionAsync-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpPendingConnection.cs:77:true
task: BL-1372
tasks: BL-1372, BL-1379
found: 2026-10-03
found-at: d1db9881553d55cd92c0e74bb41561c3dea9ea84
scorecard: 2026-10-03_1233.md
duplicate-of:
closed: 2026-10-07
closed-how: mechanical
closed-by: 2026-10-07_1336.md
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
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpPendingConnection.cs:77:true -Member TurnOffNagle -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: Mutant at TcpPendingConnection.cs:86 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86 true

## Re-audits

- 2026-10-03 | 2026-10-03_1459.md | reproduces: yes | Seed-0 mutation run: TcpPendingConnection.cs:86 `NoDelay = true` to false survived.
- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | By hand, same method as AF-0006: TcpPendingConnection.cs:77 'accepted.NoDelay = true' -> 'false'. Killed by TurnOffNagle_SetsNoDelayOnTheAcceptedSocket. This is the only 'NoDelay = true' assignment in the library.
- 2026-10-07 | 2026-10-07_1336.md | reproduces: no | Ran the -Site reproduction: resolvedLine 77, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
- 2026-10-07: accepted -> closed. Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
