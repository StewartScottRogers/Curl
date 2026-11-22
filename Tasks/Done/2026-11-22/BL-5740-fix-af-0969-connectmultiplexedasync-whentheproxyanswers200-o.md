---
id: BL-5740
title: Fix AF-0969: ConnectMultiplexedAsync_WhenTheProxyAnswers200_OpensTheTunnel checks a log line, never that a tunnel connection was opened
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5740 — Fix AF-0969: ConnectMultiplexedAsync_WhenTheProxyAnswers200_OpensTheTunnel checks a log line, never that a tunnel connection was opened

## Goal

The defect the audit office reported as AF-0969 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0969 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs:117`

Location: `Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs:117`

The test's only assertion is 'CollectionAssert.Contains(events.Info, "CONNECT-UDP tunnel established, response 200");' (line 117). 'await using var connection = result.Connection!;' (line 114) accepts null, so a connector that wrote the line and then returned no connection (or a failure exit code) would pass. The name promises the tunnel opens.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs -Pattern 'OpensTheTunnel' -Context 0,13
```

- Expected: The test also asserts result.ExitCode is Ok and result.Connection is the tunnel
- Actual: Only CollectionAssert.Contains(events.Info, "CONNECT-UDP tunnel established, response 200") is asserted

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-22: Doing -> Done. Test now asserts ExitCode Ok and non-null Connection; passes
