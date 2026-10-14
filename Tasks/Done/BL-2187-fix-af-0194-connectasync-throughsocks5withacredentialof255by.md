---
id: BL-2187
title: Fix AF-0194: ConnectAsync_ThroughSocks5WithACredentialOf255Bytes_SendsIt never checks what was sent, only exit 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-13
---
# BL-2187 — Fix AF-0194: ConnectAsync_ThroughSocks5WithACredentialOf255Bytes_SendsIt never checks what was sent, only exit 0

## Goal

The defect the audit office reported as AF-0194 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0194 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0194-connectasync-throughsocks5withacredentialof255byte.md`.

Location: `Curl.Networking.UnitTests/TcpConnectorTests.Socks.cs:434`

Location: `Curl.Networking.UnitTests/TcpConnectorTests.Socks.cs:434`

Step 1 sample. The body discards the proxy connection (var (result, _) = await ConnectThroughSocksAsync(...)) and its only assertion is Assert.AreEqual(CurlExitCode.Ok, result.ExitCode) (line 444). The scripted proxy answers the same whatever arrives, so a 255-byte user or password that was truncated, sent with a wrong length byte, or left out would still pass. The SOCKS4 twin at line 140 (..WithAUserNameOf255Bytes_SendsIt) does check the bytes: Assert.HasCount(8 + 255 + 1, proxyConnection.Written).

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorTests.Socks.cs -Pattern 'var \(result, _\) = await ConnectThroughSocksAsync' -Context 0,11
```

- Expected: The test keeps the proxy connection and asserts the RFC 1929 sub-negotiation bytes (0x01, 255, 255 x 'a', 255, 255 x 'p').
- Actual: Line 436 discards the connection; the only assertion is Assert.AreEqual(CurlExitCode.Ok, result.ExitCode) at line 444.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test-only fix, done directly rather than through the full feature pipeline: no production behaviour changes. `ConnectAsync_ThroughSocks5WithACredentialOf255Bytes_SendsIt` now keeps the proxy connection and asserts every byte sent: the greeting 05 03 00 01 02, the RFC 1929 sub-negotiation 01 ff + 255 x a + ff + 255 x p, then the connect request. The reproduction no longer matches this test (its one remaining match, line 31, is a different SOCKS test outside the finding). Curl.Networking.UnitTests: 3144 passed, 29 skipped; `dotnet build` clean.

## Log

- 2026-10-14: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. SOCKS5 255-byte credential test now asserts the RFC 1929 bytes sent, not just exit 0
