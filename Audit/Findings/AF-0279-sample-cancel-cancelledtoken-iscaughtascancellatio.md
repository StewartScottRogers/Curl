---
id: AF-0279
title: Sample_Cancel_CancelledToken_IsCaughtAsCancellation never checks that the cancellation branch ran
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Api.UnitTests/Readme/ReadmeTests.cs:Sample_Cancel_CancelledToken_IsCaughtAsCancellation:no-assertion
reproduction: none
task: none
tasks:
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0279 - Sample_Cancel_CancelledToken_IsCaughtAsCancellation never checks that the cancellation branch ran

## Summary

Low finding from the quality auditor at `Curl.Api.UnitTests/Readme/ReadmeTests.cs:135`: Sample_Cancel_CancelledToken_IsCaughtAsCancellation never checks that the cancellation branch ran. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:135`

The body cancels a token and awaits ReadmeSamples.CancelAndHandleErrorsAsync(client, cancelled.Token) with no assertion. The sample also catches CurlException and lets a completed transfer pass, so the test passes whether the cancellation was caught as cancellation, as a CurlException, or not at all.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Readme/ReadmeTests.cs -Pattern 'Sample_Cancel_CancelledToken_IsCaughtAsCancellation' -Context 0,8
```

- Expected: The test checks the 'Cancelled: the transfer was aborted.' line or the cancellation itself.
- Actual: The method body holds no Assert call.

## Re-audits

## Log

- 2026-10-18: filed proposed.
