---
id: AF-0342
title: TcpConnector's UDP-tunnel connect timeout > TimeSpan.Zero can become >= with no test failing, so --connect-timeout 0 times HTTP/3 through a proxy out at once
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-22_1902.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:ConnectMultiplexedThroughProxyTracedAsync-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:93:>
task: BL-3122
tasks: BL-3122
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed: 2026-10-22
closed-how: mechanical
closed-by: 2026-10-22_1902.md
---
# AF-0342 - TcpConnector's UDP-tunnel connect timeout > TimeSpan.Zero can become >= with no test failing, so --connect-timeout 0 times HTTP/3 through a proxy out at once

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:93`: TcpConnector's UDP-tunnel connect timeout > TimeSpan.Zero can become >= with no test failing, so --connect-timeout 0 times HTTP/3 through a proxy out at once.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:93`

Sampled mutant (seed 0) survived: 'connectTimeout > TimeSpan.Zero ? connectTimeout : null' became '>='. A zero connect timeout means none (ConnectTimeoutOrDefault treats it so at line 65). With the mutant, the QuicDialRequest carries TimeSpan.Zero, and QuicDialer.DialAsync answers 'remaining <= TimeSpan.Zero' with TimedOut before dialling. An HTTP/3 transfer through a UDP tunnel with a zero timeout would fail with exit 28. No test covers the zero boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:93:> -Member ConnectMultiplexedThroughProxyTracedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-10-22 | 2026-10-22_1902.md | reproduces: no | Ran the -Site reproduction: TcpConnector.UdpTunnel.cs:93:> (ConnectMultiplexedThroughProxyTracedAsync), resolvedLine 93, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-22: accepted -> closed. Re-audit 2026-10-22_1902.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
