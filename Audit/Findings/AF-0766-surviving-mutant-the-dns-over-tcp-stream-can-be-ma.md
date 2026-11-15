---
id: AF-0766
title: Surviving mutant: the DNS-over-TCP stream can be made not to own its socket, leaving it open
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/DnsSocketOpener.cs:ConnectStreamAsync-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DnsSocketOpener.cs:35:true
task: BL-5215
tasks: BL-5215
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed: 2026-11-15
closed-how: mechanical
closed-by: 2026-11-15_0222.md
---
# AF-0766 - Surviving mutant: the DNS-over-TCP stream can be made not to own its socket, leaving it open

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/DnsSocketOpener.cs:35`: Surviving mutant: the DNS-over-TCP stream can be made not to own its socket, leaving it open. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/DnsSocketOpener.cs:35`

Mutant 'return new NetworkStream(socket, ownsSocket: true);' -> 'ownsSocket: false' survived. Disposing the stream would then leave the socket open: no FIN reaches the DNS server, and one descriptor leaks per TCP lookup until the GC runs. The member is [ExcludeFromCodeCoverage] (ADR-0083, a thin socket adapter measured by the Integration run), so no fast test can catch this decision.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DnsSocketOpener.cs:35:true -Member ConnectStreamAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-15 | 2026-11-15_0222.md | reproduces: no | Ran the -Site command: resolvedLine 35, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-14: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
