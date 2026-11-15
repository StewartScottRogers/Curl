---
id: AF-0783
title: ConnectAsync_WithProxyAnyAuth_WhenNegotiateMakesNoToken_FailsWithThe407 never checks the 407
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitTests/TcpConnectorTests.ProxyNtlmAndNegotiate.cs:ConnectAsync_WithProxyAnyAuth_WhenNegotiateMakesNoToken_FailsWithThe407:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0783 - ConnectAsync_WithProxyAnyAuth_WhenNegotiateMakesNoToken_FailsWithThe407 never checks the 407

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitTests/TcpConnectorTests.ProxyNtlmAndNegotiate.cs:186`: ConnectAsync_WithProxyAnyAuth_WhenNegotiateMakesNoToken_FailsWithThe407 never checks the 407.

## Evidence

Location: `Curl.Networking.UnitTests/TcpConnectorTests.ProxyNtlmAndNegotiate.cs:186`

Step 1 sample. The body asserts CurlExitCode.CouldntConnect, the CONNECT bytes, one dial and ContextsMade, but neither result.ErrorMessage nor ProxyConnectResponseCode. Its sibling ConnectAsync_WithProxyNegotiate_WhenTheContextHasNoCredentials_FailsWithThe407 asserts "CONNECT tunnel failed, response 407". A failure naming another status would pass here.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorTests.ProxyNtlmAndNegotiate.cs -Pattern 'WhenNegotiateMakesNoToken_FailsWithThe407' -Context 0,15
```

- Expected: An assertion of the 407 (message 'CONNECT tunnel failed, response 407' or ProxyConnectResponseCode 407).
- Actual: Only the exit code CouldntConnect, the written CONNECT, the dial count and the context count are asserted.

## Re-audits

## Log

- 2026-11-15: filed proposed.
