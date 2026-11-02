---
id: BL-4445
title: Fix AF-0516: Of_SeventyKilobyteHeader_FailsTheSendWithExit55 never asserts exit 55 or the message
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-01
completed: 2026-11-01
---
# BL-4445 — Fix AF-0516: Of_SeventyKilobyteHeader_FailsTheSendWithExit55 never asserts exit 55 or the message

## Goal

The defect the audit office reported as AF-0516 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0516 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0516-of-seventykilobyteheader-failsthesendwithexit55-ne.md`.

Location: `Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs:25`

Location: `Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs:25`

The body asserts only Assert.ThrowsExactly<HttpTransferException>; the exit code and message are passed to Diagnostics.Assert (lines 25-26), which TestDiagnostics.cs:189-193 implements as AssertCount++ and a log line, never a failure. An Http2RequestHeaders.Of that threw HttpTransferException with any exit code (for example 56 instead of SendError 55) or any text would pass, though the name promises exit 55.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs -Pattern 'Assert\.AreEqual'
```

- Expected: A match: Assert.AreEqual(CurlExitCode.SendError, exception.ExitCode) and the message check.
- Actual: No match; lines 25-26 are Diagnostics.Assert("exit code", ...) and Diagnostics.Assert("message", ...) only.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Kept the Diagnostics.Assert log lines and added real `Assert.AreEqual` checks after them for exit 55 (SendError) and the message. Did the same for Of_TwentyKilobyteHeader_StillSends in the same file, which had the same flaw (its field count was only logged). The change is test-only and platform-neutral, so no quality run was needed: no production code changed.
- Verified: the reproduction's Select-String now matches lines 27, 28 and 39; `dotnet build` gives 0 errors; the fast tests had no failing project.

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-01: Doing -> Done. Cgr271Tests now asserts exit 55 and the send-failure message with Assert.AreEqual
