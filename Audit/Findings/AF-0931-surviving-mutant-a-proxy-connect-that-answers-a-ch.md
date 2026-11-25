---
id: AF-0931
title: Surviving mutant: a proxy CONNECT that answers a challenge on the same connection can be reported as not answering it
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/TcpConnector.cs:NetworkDiagnosticLog-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpConnector.cs:1926:true
task: BL-5690
tasks: BL-5690
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed: 2026-11-21
closed-how: mechanical
closed-by: 2026-11-21_1740.md
---
# AF-0931 - Surviving mutant: a proxy CONNECT that answers a challenge on the same connection can be reported as not answering it

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpConnector.cs:1926`: Surviving mutant: a proxy CONNECT that answers a challenge on the same connection can be reported as not answering it. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:1926`

Seed 812510 sample: answersChallenge = true; mutated to answersChallenge = false; survived (the tool names the member NetworkDiagnosticLog). answersChallenge goes to RequestTunnelAsync and ConnectTunnelVerboseLines.ReportBeforeConnect (TcpConnector.cs:2100), so the repeated CONNECT after a 407 writes other verbose lines and is treated as a first request. No test checks that second CONNECT.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:1926:true -Member NetworkDiagnosticLog -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-21 | 2026-11-21_1740.md | reproduces: no | Ran the -Site command: resolvedLine 1926, mutant 'answersChallenge = false;' killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-21: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
