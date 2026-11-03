---
id: BL-4753
title: Fix AF-0589: ExecuteAsync_AllowFailsThenDataRefused_IsExit55 never checks exit 55
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4753 — Fix AF-0589: ExecuteAsync_AllowFailsThenDataRefused_IsExit55 never checks exit 55

## Goal

The defect the audit office reported as AF-0589 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0589 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0589-executeasync-allowfailsthendatarefused-isexit55-ne.md`.

Location: `Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerMailExtensionTests.cs:313`

Location: `Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerMailExtensionTests.cs:313`

Step 0 name-lies. The test asserts the bytes sent and Assert.AreEqual("DATA failed: 554", run.Result.ErrorMessage); run.Result.ExitCode (SendError, 55) is never checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Smtp.UnitTests/SmtpProtocolHandlerMailExtensionTests.cs -Pattern 'AllowFailsThenDataRefused_IsExit55' -Context 0,12
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.SendError, run.Result.ExitCode).
- Actual: Only run.Sent and run.Result.ErrorMessage are asserted.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added the exit-code assertion (SendError, 55) to the test; Smtp tests pass (405 passed).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Added the SendError exit-code assertion; Smtp tests green.
