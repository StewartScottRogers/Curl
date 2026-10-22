---
id: BL-3122
title: Fix AF-0342: TcpConnector's UDP-tunnel connect timeout > TimeSpan.Zero can become >= with no test failing, so --connect-timeout 0 times HTTP/3 through a proxy out at once
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3122 — Fix AF-0342: TcpConnector's UDP-tunnel connect timeout > TimeSpan.Zero can become >= with no test failing, so --connect-timeout 0 times HTTP/3 through a proxy out at once

## Goal

The defect the audit office reported as AF-0342 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0342 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0342-tcpconnector-s-udp-tunnel-connect-timeout-timespan.md`.

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:93`

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:93`

Sampled mutant (seed 0) survived: 'connectTimeout > TimeSpan.Zero ? connectTimeout : null' became '>='. A zero connect timeout means none (ConnectTimeoutOrDefault treats it so at line 65). With the mutant, the QuicDialRequest carries TimeSpan.Zero, and QuicDialer.DialAsync answers 'remaining <= TimeSpan.Zero' with TimedOut before dialling. An HTTP/3 transfer through a UDP tunnel with a zero timeout would fail with exit 28. No test covers the zero boundary.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:93:> -Member ConnectMultiplexedThroughProxyTracedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-22 (lane 2): The fix is a test, not a production change: the mutant survives because no test dials HTTP/3 through a UDP tunnel with `connectTimeout: TimeSpan.Zero`. Add one to `Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs` (model it on `TcpConnectorQuicTests.cs:284`, the direct-dial zero-timeout test) asserting the tunnelled dial is not timed out at once (the QuicDialRequest carries no timeout). Added that file to touches; it is in Curl.Networking.UnitTests, which BL-3127 holds whole, so requeued behind BL-3127 before any edit. A lane cannot run the finding reproduction (the audit tools are refused to lanes); the re-audit runs it.

- 2026-10-22 (lane 6): Added `ConnectMultiplexedAsync_ThroughAnHttpProxyWithAZeroConnectTimeout_RunsQuicInTheTunnelWithoutATimeout` to `TcpConnectorQuicTests.UdpTunnel.cs`: an HTTP-proxy CONNECT-UDP dial with `connectTimeout: TimeSpan.Zero` completes the QUIC handshake (exit 0, h3). Lanes may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand (`>` to `>=` at `TcpConnector.UdpTunnel.cs:93`): the new test failed (killed), and the file was restored. No production change. Build clean, fast tests green (44 assemblies, 0 failed); the test is platform-neutral (in-memory fakes, manual clock), so CI on Linux and macOS is covered by that.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Backlog. Needs Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs for the zero-timeout test; BL-3127 holds Curl.Networking.UnitTests
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Done. A zero-connect-timeout HTTP/3 dial through a UDP tunnel is tested and kills the > to >= mutant
