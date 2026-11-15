---
id: BL-5234
title: Fix AF-0783: ConnectAsync_WithProxyAnyAuth_WhenNegotiateMakesNoToken_FailsWithThe407 never checks the 407
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-15
completed:
---
# BL-5234 — Fix AF-0783: ConnectAsync_WithProxyAnyAuth_WhenNegotiateMakesNoToken_FailsWithThe407 never checks the 407

## Goal

The defect the audit office reported as AF-0783 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0783 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitTests/TcpConnectorTests.ProxyNtlmAndNegotiate.cs:186`

Location: `Curl.Networking.UnitTests/TcpConnectorTests.ProxyNtlmAndNegotiate.cs:186`

Step 1 sample. The body asserts CurlExitCode.CouldntConnect, the CONNECT bytes, one dial and ContextsMade, but neither result.ErrorMessage nor ProxyConnectResponseCode. Its sibling ConnectAsync_WithProxyNegotiate_WhenTheContextHasNoCredentials_FailsWithThe407 asserts "CONNECT tunnel failed, response 407". A failure naming another status would pass here.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorTests.ProxyNtlmAndNegotiate.cs -Pattern 'WhenNegotiateMakesNoToken_FailsWithThe407' -Context 0,15
```

- Expected: An assertion of the 407 (message 'CONNECT tunnel failed, response 407' or ProxyConnectResponseCode 407).
- Actual: Only the exit code CouldntConnect, the written CONNECT, the dial count and the context count are asserted.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
