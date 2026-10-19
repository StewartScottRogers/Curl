---
id: BL-2805
title: Fix AF-0278: Sample_Cancel_SucceedsWhenNothingFails cannot fail: the sample it runs catches every cancellation and CurlException
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2805 — Fix AF-0278: Sample_Cancel_SucceedsWhenNothingFails cannot fail: the sample it runs catches every cancellation and CurlException

## Goal

The defect the audit office reported as AF-0278 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0278 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0278-sample-cancel-succeedswhennothingfails-cannot-fail.md`.

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:127`

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:127`

The test only awaits ReadmeSamples.CancelAndHandleErrorsAsync(client, CancellationToken.None). That sample (ReadmeSamples.cs:77-97) wraps the send in try/catch (OperationCanceledException) and catch (CurlException) and only prints to Console. A transfer that failed or was cancelled passes, so 'succeeds' is never checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Readme/ReadmeSamples.cs -Pattern 'catch \((OperationCanceledException|CurlException)'
```

- Expected: The test asserts the transfer succeeded, for example the body was read and no error line was written.
- Actual: Two catch clauses in the sample swallow both failure kinds; the test has no assertion.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
