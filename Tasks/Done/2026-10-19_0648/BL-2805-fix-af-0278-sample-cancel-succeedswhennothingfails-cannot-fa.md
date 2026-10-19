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
completed: 2026-10-19
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Done directly rather than the full /feature pipeline: a one-test change in a test project. The sample stays as it is because `Curl.Api/README.md` shows it verbatim (README test keeps them identical) and the README is outside `touches`.
- `Sample_Cancel_SucceedsWhenNothingFails` now captures Console output and asserts it is empty (no "Cancelled" or "curl error" line), that the transfer hit `https://example.com/slow`, that all 5 body bytes were taken, and that the handle was disposed. A failed or cancelled transfer now fails it.
- Marked `[DoNotParallelize]`: `Console.SetOut` is process-wide and the assembly runs method-level parallel, so other samples' lines could otherwise land in the writer.
- The reproduction's Select-String still lists the two catch clauses (they are the sample's point); the expected result - the test asserts the transfer succeeded - now holds.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-19: Doing -> Done. Sample_Cancel_SucceedsWhenNothingFails now asserts no error line, the body read and the handle disposed
