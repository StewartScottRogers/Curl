---
id: AF-0116
title: The CONNECT-UDP reply's verbose lines can be rendered as a CONNECT reply's (forConnectUdp: true -> false) with no test failing
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:RequestUdpTunnelAsync-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:244:true
task: BL-1866
tasks: BL-1866
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0116 - The CONNECT-UDP reply's verbose lines can be rendered as a CONNECT reply's (forConnectUdp: true -> false) with no test failing

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:244`: The CONNECT-UDP reply's verbose lines can be rendered as a CONNECT reply's (forConnectUdp: true -> false) with no test failing. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:244`

Mutant 'ConnectTunnelVerboseLines.ReportReplyHead(..., forConnectUdp: true)' -> 'forConnectUdp: false' survived. In ConnectTunnelVerboseLines.ReportReplyHead (line 156-160) the flag picks HttpProxyTunnel.IgnoresBodyFields(statusCode, forConnectUdp) and the label of the ignored-field line, ' in CONNECT-UDP <code> response' vs ' in CONNECT <code> response' (measured, BL-1399). So curl -v through an HTTP/3 UDP tunnel would write a different line to stderr. The UDP-tunnel tests do not pin the reply-head lines for a reply that carries body fields.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:244:true -Member RequestUdpTunnelAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Ran the -Site reproduction: survived TcpConnector.UdpTunnel.cs:244 true [RequestUdpTunnelAsync] forConnectUdp: true -> false. It survived in the seed-0 sample as well.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
