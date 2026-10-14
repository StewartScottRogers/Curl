---
id: AF-0194
title: ConnectAsync_ThroughSocks5WithACredentialOf255Bytes_SendsIt never checks what was sent, only exit 0
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitTests/TcpConnectorTests.Socks.cs:ConnectAsync_ThroughSocks5WithACredentialOf255Bytes_SendsIt:name-lies
reproduction: none
task: none
tasks:
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0194 - ConnectAsync_ThroughSocks5WithACredentialOf255Bytes_SendsIt never checks what was sent, only exit 0

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitTests/TcpConnectorTests.Socks.cs:434`: ConnectAsync_ThroughSocks5WithACredentialOf255Bytes_SendsIt never checks what was sent, only exit 0.

## Evidence

Location: `Curl.Networking.UnitTests/TcpConnectorTests.Socks.cs:434`

Step 1 sample. The body discards the proxy connection (var (result, _) = await ConnectThroughSocksAsync(...)) and its only assertion is Assert.AreEqual(CurlExitCode.Ok, result.ExitCode) (line 444). The scripted proxy answers the same whatever arrives, so a 255-byte user or password that was truncated, sent with a wrong length byte, or left out would still pass. The SOCKS4 twin at line 140 (..WithAUserNameOf255Bytes_SendsIt) does check the bytes: Assert.HasCount(8 + 255 + 1, proxyConnection.Written).

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorTests.Socks.cs -Pattern 'var \(result, _\) = await ConnectThroughSocksAsync' -Context 0,11
```

- Expected: The test keeps the proxy connection and asserts the RFC 1929 sub-negotiation bytes (0x01, 255, 255 x 'a', 255, 255 x 'p').
- Actual: Line 436 discards the connection; the only assertion is Assert.AreEqual(CurlExitCode.Ok, result.ExitCode) at line 444.

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
