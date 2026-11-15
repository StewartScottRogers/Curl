---
id: AF-0763
title: DialAsync_HandshakeFails_ThrowsItsExitCodeAndDisposesTheChannel checks only that the exit code is not Ok
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces.
key: quality:Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportOtherTests.cs:DialAsync_HandshakeFails_ThrowsItsExitCodeAndDisposesTheChannel:weak-assertion
reproduction: none
task: BL-5212
tasks: BL-5212
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed: 2026-11-15
closed-how: reliable-reaudit
closed-by: 2026-11-15_0222.md
---
# AF-0763 - DialAsync_HandshakeFails_ThrowsItsExitCodeAndDisposesTheChannel checks only that the exit code is not Ok

## Summary

Low finding from the quality auditor at `Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportOtherTests.cs:158`: DialAsync_HandshakeFails_ThrowsItsExitCodeAndDisposesTheChannel checks only that the exit code is not Ok. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportOtherTests.cs:158`

The name promises the handshake's own exit code, but line 158 is Assert.AreNotEqual(CurlExitCode.Ok, failure.ExitCode). The server closes after the ClientHello with code 0x0a (line 153), yet any failing code (for example 7 or 56 in place of 35) passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportOtherTests.cs -Pattern 'Assert.AreNotEqual\(CurlExitCode.Ok, failure.ExitCode\)'
```

- Expected: No match: the test asserts the exact exit code a refused QUIC handshake gives.
- Actual: WebTransportOtherTests.cs:158:        Assert.AreNotEqual(CurlExitCode.Ok, failure.ExitCode);

## Re-audits

- 2026-11-15 | 2026-11-15_0222.md | reproduces: no | Ran the Select-String: no match. Line 166 asserts Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode).

## Log

- 2026-11-14: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces.
