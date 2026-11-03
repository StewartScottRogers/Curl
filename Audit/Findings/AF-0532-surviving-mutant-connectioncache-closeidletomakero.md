---
id: AF-0532
title: Surviving mutant: ConnectionCache.CloseIdleToMakeRoomAsync can be off by two on the connection limit without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_0025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/ConnectionCache.cs:CloseIdleToMakeRoomAsync-minus1:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ConnectionCache.cs:123:-1
task: BL-4638
tasks: BL-4638
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_0025.md
---
# AF-0532 - Surviving mutant: ConnectionCache.CloseIdleToMakeRoomAsync can be off by two on the connection limit without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ConnectionCache.cs:123`: Surviving mutant: ConnectionCache.CloseIdleToMakeRoomAsync can be off by two on the connection limit without a test failing. Reported by an auditor flagged unreliable in 2026-11-02_1724.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/ConnectionCache.cs:123`

Mutant 'Idle.Count + Leased.Count - Math.Max(limit - 1, 0)' -> 'Math.Max(limit + 1, 0)' survived (Curl.Networking.UnitTests green). The method enforces CURLMOPT_MAX_TOTAL_CONNECTIONS (CGR-150) by closing idle connections to make room. With the mutant, two more connections than the limit stay open before any idle one is closed, which changes how many sockets are open and which connections are reused or closed (the verbose closing lines).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ConnectionCache.cs:123:-1 -Member CloseIdleToMakeRoomAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-03 | 2026-11-03_0025.md | reproduces: no | Ran the -Site command: outcome killed at resolvedLine 123 ('Math.Max(limit - 1, 0)' -> 'Math.Max(limit + 1, 0)'); the baseline left out three failing ECH tests. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_0025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
