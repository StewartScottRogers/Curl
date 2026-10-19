---
id: AF-0278
title: Sample_Cancel_SucceedsWhenNothingFails cannot fail: the sample it runs catches every cancellation and CurlException
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Api.UnitTests/Readme/ReadmeTests.cs:Sample_Cancel_SucceedsWhenNothingFails:swallowed-failure
reproduction: none
task: BL-2805
tasks: BL-2805
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0278 - Sample_Cancel_SucceedsWhenNothingFails cannot fail: the sample it runs catches every cancellation and CurlException

## Summary

Low finding from the quality auditor at `Curl.Api.UnitTests/Readme/ReadmeTests.cs:127`: Sample_Cancel_SucceedsWhenNothingFails cannot fail: the sample it runs catches every cancellation and CurlException. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:127`

The test only awaits ReadmeSamples.CancelAndHandleErrorsAsync(client, CancellationToken.None). That sample (ReadmeSamples.cs:77-97) wraps the send in try/catch (OperationCanceledException) and catch (CurlException) and only prints to Console. A transfer that failed or was cancelled passes, so 'succeeds' is never checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Readme/ReadmeSamples.cs -Pattern 'catch \((OperationCanceledException|CurlException)'
```

- Expected: The test asserts the transfer succeeded, for example the body was read and no error line was written.
- Actual: Two catch clauses in the sample swallow both failure kinds; the test has no assertion.

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
