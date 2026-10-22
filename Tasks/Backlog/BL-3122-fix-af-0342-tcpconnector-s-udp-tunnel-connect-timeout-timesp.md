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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-22 (lane 2): The fix is a test, not a production change: the mutant survives because no test dials HTTP/3 through a UDP tunnel with `connectTimeout: TimeSpan.Zero`. Add one to `Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs` (model it on `TcpConnectorQuicTests.cs:284`, the direct-dial zero-timeout test) asserting the tunnelled dial is not timed out at once (the QuicDialRequest carries no timeout). Added that file to touches; it is in Curl.Networking.UnitTests, which BL-3127 holds whole, so requeued behind BL-3127 before any edit. A lane cannot run the finding reproduction (the audit tools are refused to lanes); the re-audit runs it.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Backlog. Needs Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs for the zero-timeout test; BL-3127 holds Curl.Networking.UnitTests
