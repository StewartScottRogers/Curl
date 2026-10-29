---
id: BL-4177
title: Fix AF-0454: TcpConnector.SecureWhenAskedAsync's default writesSslFilterAdded = false can become true with no test failing, adding an SSL-filter line to -v output after every CONNECT tunnel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/TcpConnectorTests.Http2ProxyTunnel.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4177 — Fix AF-0454: TcpConnector.SecureWhenAskedAsync's default writesSslFilterAdded = false can become true with no test failing, adding an SSL-filter line to -v output after every CONNECT tunnel

## Goal

The defect the audit office reported as AF-0454 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0454 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0454-tcpconnector-securewhenaskedasync-s-default-writes.md`.

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:2169`

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:2169`

Line 2169: `bool writesSslFilterAdded = false)`. Every other caller passes WritesSslFilterAddedFor(target), which is `TracesSetupFilter && IsHttpsOrigin(target)` (line 850). Only the tunnel path at line 1726 (`return await SecureWhenAskedAsync(dialed with { Connection = tunnelConnection }, tunnel.Target, timings, statusCode, cancellationToken)`) relies on the default. With the default flipped to true, AuthenticateTargetAsync (line 2391) calls `target.Events.ReportInfo(SslFilterAddedLine)`, so `* [SETUP] added SSL filter for origin` appears in the verbose output of every transfer through an HTTP CONNECT tunnel, even with no --trace-config. The sampled mutation run (seed 0) left this mutant surviving: no Curl.Networking.UnitTests test checks the tunnel path's verbose lines without setup tracing.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:2169:false -Member SecureWhenAskedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a tunnel test without setup tracing sees no SSL-filter-added line.
- Actual: outcome survived: every Curl.Networking.UnitTests test passed with the mutant.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Only the HTTP/2 CONNECT tunnel (OpenHttp2TunnelAsync, the h2 proxy under --proxy-http2) relies on the default; no test drove it to a TLS origin. Added ConnectAsync_ThroughAnH2TunnelToAnHttpsOriginWithoutSetupTracing_WritesNoSslFilterAddedLine in Curl.Networking.UnitTests/TcpConnectorTests.Http2ProxyTunnel.cs (added to touches; no other Doing task names it). Production code unchanged, so no quality run was needed.
- Reproduction: a lane may not run the audit office mutation tool, so the mutant was applied by hand (line 2169 default flipped to true): the new test fails (killed), and passes with the code restored. Build clean; all 50 fast test projects green; the test is platform-neutral (fakes only).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-29: Doing -> Done. A test through the h2 CONNECT tunnel to an HTTPS origin without setup tracing now kills AF-0454's mutant
