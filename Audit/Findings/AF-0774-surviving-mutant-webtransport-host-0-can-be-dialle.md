---
id: AF-0774
title: Surviving mutant: webtransport://host:0/ can be dialled on UDP 443
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportRequest.cs:PortOf-ge:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportRequest.cs:35:>=
task: BL-5223
tasks: BL-5223
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed: 2026-11-15
closed-how: mechanical
closed-by: 2026-11-15_0222.md
---
# AF-0774 - Surviving mutant: webtransport://host:0/ can be dialled on UDP 443

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportRequest.cs:35`: Surviving mutant: webtransport://host:0/ can be dialled on UDP 443. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportRequest.cs:35`

Mutant 'url.Port >= 0 ? url.Port : DefaultPort' -> 'url.Port > 0' survived. Of_NoPort_DialsUdp443AndSendsTheHostAlone and Of_WtField_ChoosesTheModeAndIsNotSent cover no port and :8443 only. Under the mutant an explicit port 0 dials 443, while AuthorityOf (line 37) still writes host:0, so the dial target and the :authority disagree.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportRequest.cs:35:>= -Member PortOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-15 | 2026-11-15_0222.md | reproduces: no | Ran the -Site command: resolvedLine 35, outcome killed, score 1. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-14: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
