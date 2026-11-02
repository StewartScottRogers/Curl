---
id: AF-0516
title: Of_SeventyKilobyteHeader_FailsTheSendWithExit55 never asserts exit 55 or the message
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-01_1801.md: the reproduction no longer reproduces.
key: quality:Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs:Of_SeventyKilobyteHeader_FailsTheSendWithExit55:name-lies
reproduction: none
task: BL-4445
tasks: BL-4445
found: 2026-11-01
found-at: 8dd012a88d26bb19a4e7d95f0be5a3371e3d2b15
scorecard: 2026-11-01_0144.md
duplicate-of:
closed: 2026-11-01
closed-how: reliable-reaudit
closed-by: 2026-11-01_1801.md
---
# AF-0516 - Of_SeventyKilobyteHeader_FailsTheSendWithExit55 never asserts exit 55 or the message

## Summary

Medium finding from the quality auditor at `Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs:25`: Of_SeventyKilobyteHeader_FailsTheSendWithExit55 never asserts exit 55 or the message.

## Evidence

Location: `Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs:25`

The body asserts only Assert.ThrowsExactly<HttpTransferException>; the exit code and message are passed to Diagnostics.Assert (lines 25-26), which TestDiagnostics.cs:189-193 implements as AssertCount++ and a log line, never a failure. An Http2RequestHeaders.Of that threw HttpTransferException with any exit code (for example 56 instead of SendError 55) or any text would pass, though the name promises exit 55.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs -Pattern 'Assert\.AreEqual'
```

- Expected: A match: Assert.AreEqual(CurlExitCode.SendError, exception.ExitCode) and the message check.
- Actual: No match; lines 25-26 are Diagnostics.Assert("exit code", ...) and Diagnostics.Assert("message", ...) only.

## Re-audits

- 2026-11-01 | 2026-11-01_1016.md | reproduces: no | Select-String found Assert.AreEqual(CurlExitCode.SendError, exception.ExitCode) and Assert.AreEqual("Failed sending data to the peer", exception.Message) at Cgr271Tests.cs:27-28 in Of_SeventyKilobyteHeader_FailsTheSendWithExit55.
- 2026-11-01 | 2026-11-01_1801.md | reproduces: no | Select-String found Cgr271Tests.cs:27 Assert.AreEqual(CurlExitCode.SendError, exception.ExitCode) and :28 Assert.AreEqual("Failed sending data to the peer", exception.Message) in Of_SeventyKilobyteHeader_FailsTheSendWithExit55.

## Log

- 2026-11-01: filed proposed.
- 2026-11-01: proposed -> accepted. Stewart: accept all.
- 2026-11-01: accepted -> closed. Re-audit 2026-11-01_1801.md: the reproduction no longer reproduces.
