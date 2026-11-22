---
id: AF-0969
title: ConnectMultiplexedAsync_WhenTheProxyAnswers200_OpensTheTunnel checks a log line, never that a tunnel connection was opened
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs:ConnectMultiplexedAsync_WhenTheProxyAnswers200_OpensTheTunnel:weak-assertion
reproduction: none
task: BL-5740
tasks: BL-5740
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0969 - ConnectMultiplexedAsync_WhenTheProxyAnswers200_OpensTheTunnel checks a log line, never that a tunnel connection was opened

## Summary

Low finding from the quality auditor at `Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs:117`: ConnectMultiplexedAsync_WhenTheProxyAnswers200_OpensTheTunnel checks a log line, never that a tunnel connection was opened.

## Evidence

Location: `Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs:117`

The test's only assertion is 'CollectionAssert.Contains(events.Info, "CONNECT-UDP tunnel established, response 200");' (line 117). 'await using var connection = result.Connection!;' (line 114) accepts null, so a connector that wrote the line and then returned no connection (or a failure exit code) would pass. The name promises the tunnel opens.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs -Pattern 'OpensTheTunnel' -Context 0,13
```

- Expected: The test also asserts result.ExitCode is Ok and result.Connection is the tunnel
- Actual: Only CollectionAssert.Contains(events.Info, "CONNECT-UDP tunnel established, response 200") is asserted

## Re-audits

## Log

- 2026-11-22: filed proposed.
