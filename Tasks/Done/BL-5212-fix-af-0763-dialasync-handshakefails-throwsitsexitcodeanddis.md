---
id: BL-5212
title: Fix AF-0763: DialAsync_HandshakeFails_ThrowsItsExitCodeAndDisposesTheChannel checks only that the exit code is not Ok
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-14
completed: 2026-11-15
---
# BL-5212 — Fix AF-0763: DialAsync_HandshakeFails_ThrowsItsExitCodeAndDisposesTheChannel checks only that the exit code is not Ok

## Goal

The defect the audit office reported as AF-0763 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0763 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportOtherTests.cs:158`

Location: `Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportOtherTests.cs:158`

The name promises the handshake's own exit code, but line 158 is Assert.AreNotEqual(CurlExitCode.Ok, failure.ExitCode). The server closes after the ClientHello with code 0x0a (line 153), yet any failing code (for example 7 or 56 in place of 35) passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportOtherTests.cs -Pattern 'Assert.AreNotEqual\(CurlExitCode.Ok, failure.ExitCode\)'
```

- Expected: No match: the test asserts the exact exit code a refused QUIC handshake gives.
- Actual: WebTransportOtherTests.cs:158:        Assert.AreNotEqual(CurlExitCode.Ok, failure.ExitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Test now asserts CouldntConnect (7): the server closing a handshake with a non-CONNECTION_REFUSED code maps to 7 (QuicHandshakeFailures.FromServerClose). Test-only change; no production code.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-15: Doing -> Done. Test pins CouldntConnect (7) for a refused QUIC handshake
