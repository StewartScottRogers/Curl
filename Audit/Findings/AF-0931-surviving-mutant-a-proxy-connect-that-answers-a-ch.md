---
id: AF-0931
title: Surviving mutant: a proxy CONNECT that answers a challenge on the same connection can be reported as not answering it
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/TcpConnector.cs:NetworkDiagnosticLog-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpConnector.cs:1926:true
task: BL-5690
tasks: BL-5690
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-21: filed proposed.
