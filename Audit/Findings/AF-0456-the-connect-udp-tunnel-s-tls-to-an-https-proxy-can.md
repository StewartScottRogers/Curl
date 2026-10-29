---
id: AF-0456
title: The CONNECT-UDP tunnel's TLS to an HTTPS proxy can drop isProxy: true with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:ConnectTimings-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:166:true
task: BL-4179
tasks: BL-4179
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0456 - The CONNECT-UDP tunnel's TLS to an HTTPS proxy can drop isProxy: true with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:166`: The CONNECT-UDP tunnel's TLS to an HTTPS proxy can drop isProxy: true with no test failing. Reported by an auditor flagged unreliable in 2026-10-29_0606.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:166`

Line 166: `var secured = await AuthenticateAsync(_proxyTlsProvider, dialed.Connection, proxy.Host, target, isProxy: true, applicationProtocols: HttpApplicationProtocols.Http11Only, cancellationToken)`. The mutation run (seed 0) changed `true` to `false` and every Curl.Networking.UnitTests test passed. isProxy goes on to the TLS provider (TcpConnector.cs:2482 `reportingProvider.AuthenticateAsClientAsync(plaintext, host, ..., isProxy, ...)`), which uses it to report the certificate and verify result as the proxy's rather than the origin's. So the HTTP/3-over-HTTPS-proxy handshake would be reported and judged as an origin handshake, and no test notices. The tool names the member ConnectTimings (the nearest declaration above).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:166:true -Member ConnectTimings -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a CONNECT-UDP-over-HTTPS-proxy test sees the proxy handshake made as the proxy's.
- Actual: outcome survived: every Curl.Networking.UnitTests test passed with the mutant.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
