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
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
