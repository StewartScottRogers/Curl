---
id: BL-2806
title: Fix AF-0279: Sample_Cancel_CancelledToken_IsCaughtAsCancellation never checks that the cancellation branch ran
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitTests]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2806 — Fix AF-0279: Sample_Cancel_CancelledToken_IsCaughtAsCancellation never checks that the cancellation branch ran

## Goal

The defect the audit office reported as AF-0279 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0279 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0279-sample-cancel-cancelledtoken-iscaughtascancellatio.md`.

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:135`

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:135`

The body cancels a token and awaits ReadmeSamples.CancelAndHandleErrorsAsync(client, cancelled.Token) with no assertion. The sample also catches CurlException and lets a completed transfer pass, so the test passes whether the cancellation was caught as cancellation, as a CurlException, or not at all.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Readme/ReadmeTests.cs -Pattern 'Sample_Cancel_CancelledToken_IsCaughtAsCancellation' -Context 0,8
```

- Expected: The test checks the 'Cancelled: the transfer was aborted.' line or the cancellation itself.
- Actual: The method body holds no Assert call.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now captures Console.Out (as Sample_Cancel_SucceedsWhenNothingFails does, with [DoNotParallelize]) and asserts the sample wrote exactly "Cancelled: the transfer was aborted.", so a CurlException line or a completed transfer fails it.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-19: Doing -> Done. Sample_Cancel_CancelledToken_IsCaughtAsCancellation now asserts the sample wrote the cancellation line
