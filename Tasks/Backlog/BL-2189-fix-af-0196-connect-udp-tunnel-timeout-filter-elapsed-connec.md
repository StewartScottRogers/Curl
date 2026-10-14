---
id: BL-2189
title: Fix AF-0196: CONNECT-UDP tunnel timeout filter 'elapsed >= connectLimit' can become '>' with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2189 — Fix AF-0196: CONNECT-UDP tunnel timeout filter 'elapsed >= connectLimit' can become '>' with no test failing

## Goal

The defect the audit office reported as AF-0196 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0196 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0196-connect-udp-tunnel-timeout-filter-elapsed-connectl.md`.

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:79`

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:79`

Step 3, same sample: 'catch (OperationCanceledException) when (timeProvider.GetElapsedTime(started) >= connectLimit)' mutated to '>' survived. The filter turns the tunnel's connect-timeout cancellation into exit 28 'Connection timed out after N milliseconds'. When it does not match, the OperationCanceledException leaves ConnectMultiplexedAsync instead of exit 28. The timeout tests in TcpConnectorQuicTests.UdpTunnel.cs (lines 285-288, 321-322) advance the fake clock to 1001 ms against a 1000 ms limit, so the boundary curl uses (expiry once elapsed reaches the limit) is never tested.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:79:>= -Member ConnectMultiplexedThroughProxyTracedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
