---
id: BL-4750
title: Fix AF-0586: ExecuteAsync_NegativeCSeq_FailsWith85NamingIt never checks exit 85
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4750 — Fix AF-0586: ExecuteAsync_NegativeCSeq_FailsWith85NamingIt never checks exit 85

## Goal

The defect the audit office reported as AF-0586 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0586 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0586-executeasync-negativecseq-failswith85namingit-neve.md`.

Location: `Curl.Original.Protocol.Rtsp.UnitTests/RtspProtocolHandlerTests.cs:761`

Location: `Curl.Original.Protocol.Rtsp.UnitTests/RtspProtocolHandlerTests.cs:761`

Step 0 name-lies. The only assertion is Assert.AreEqual("The CSeq of this request 1 did not match the response -3", result.ErrorMessage); result.ExitCode (RtspCseqError, 85) is never checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Rtsp.UnitTests/RtspProtocolHandlerTests.cs -Pattern 'NegativeCSeq_FailsWith85NamingIt' -Context 0,7
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.RtspCseqError, result.ExitCode).
- Actual: Only result.ErrorMessage is asserted.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added the exit-code assertion (RtspCseqError, 85) to the test; Rtsp fast tests pass (369 passed). Test-only change, platform-neutral.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Added exit 85 assertion
