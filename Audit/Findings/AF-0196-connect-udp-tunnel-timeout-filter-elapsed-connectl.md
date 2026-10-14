---
id: AF-0196
title: CONNECT-UDP tunnel timeout filter 'elapsed >= connectLimit' can become '>' with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-14_0453.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:ConnectMultiplexedThroughProxyTracedAsync-ge:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:79:>=
task: BL-2189
tasks: BL-2189
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed: 2026-10-14
closed-how: mechanical
closed-by: 2026-10-14_0453.md
---
# AF-0196 - CONNECT-UDP tunnel timeout filter 'elapsed >= connectLimit' can become '>' with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:79`: CONNECT-UDP tunnel timeout filter 'elapsed >= connectLimit' can become '>' with no test failing.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:79`

Step 3, same sample: 'catch (OperationCanceledException) when (timeProvider.GetElapsedTime(started) >= connectLimit)' mutated to '>' survived. The filter turns the tunnel's connect-timeout cancellation into exit 28 'Connection timed out after N milliseconds'. When it does not match, the OperationCanceledException leaves ConnectMultiplexedAsync instead of exit 28. The timeout tests in TcpConnectorQuicTests.UdpTunnel.cs (lines 285-288, 321-322) advance the fake clock to 1001 ms against a 1000 ms limit, so the boundary curl uses (expiry once elapsed reaches the limit) is never tested.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:79:>= -Member ConnectMultiplexedThroughProxyTracedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-14 | 2026-10-14_0453.md | reproduces: no | Ran the -Site reproduction (Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:79:>=, -Member ConnectMultiplexedThroughProxyTracedAsync, -ExcludeBaselineFailures, -TimeoutSeconds 600): resolvedLine 79, mutant 'GetElapsedTime(started) >= connectLimit' -> '> connectLimit', outcome killed. Same four TcpConnectorQuicTests excluded as baseline failures. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
- 2026-10-14: accepted -> closed. Re-audit 2026-10-14_0453.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
