---
id: AF-0716
title: Surviving mutant: libcurl's own TCP socket can be opened without TCP_NODELAY
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:Open-true:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:339:true
task: BL-5075
tasks: BL-5075
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: mechanical
closed-by: 2026-11-13_2321.md
---
# AF-0716 - Surviving mutant: libcurl's own TCP socket can be opened without TCP_NODELAY

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:339`: Surviving mutant: libcurl's own TCP socket can be opened without TCP_NODELAY. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:339`

Mutant '{ NoDelay = true }' -> '{ NoDelay = false }' survived. curl sets TCP_NODELAY by default (--tcp-nodelay is on). No test reads the option back from the socket SocketCallbackConnector opens.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/SocketCallbackConnector.cs:339:true -Member Open -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Ran the -Site command: outcome killed (NoDelay = true -> false). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
