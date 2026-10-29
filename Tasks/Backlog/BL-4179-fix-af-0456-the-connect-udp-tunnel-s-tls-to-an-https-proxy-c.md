---
id: BL-4179
title: Fix AF-0456: The CONNECT-UDP tunnel's TLS to an HTTPS proxy can drop isProxy: true with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-29
completed:
---
# BL-4179 — Fix AF-0456: The CONNECT-UDP tunnel's TLS to an HTTPS proxy can drop isProxy: true with no test failing

## Goal

The defect the audit office reported as AF-0456 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0456 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0456-the-connect-udp-tunnel-s-tls-to-an-https-proxy-can.md`.

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:166`

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:166`

Line 166: `var secured = await AuthenticateAsync(_proxyTlsProvider, dialed.Connection, proxy.Host, target, isProxy: true, applicationProtocols: HttpApplicationProtocols.Http11Only, cancellationToken)`. The mutation run (seed 0) changed `true` to `false` and every Curl.Networking.UnitTests test passed. isProxy goes on to the TLS provider (TcpConnector.cs:2482 `reportingProvider.AuthenticateAsClientAsync(plaintext, host, ..., isProxy, ...)`), which uses it to report the certificate and verify result as the proxy's rather than the origin's. So the HTTP/3-over-HTTPS-proxy handshake would be reported and judged as an origin handshake, and no test notices. The tool names the member ConnectTimings (the nearest declaration above).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:166:true -Member ConnectTimings -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a CONNECT-UDP-over-HTTPS-proxy test sees the proxy handshake made as the proxy's.
- Actual: outcome survived: every Curl.Networking.UnitTests test passed with the mutant.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
