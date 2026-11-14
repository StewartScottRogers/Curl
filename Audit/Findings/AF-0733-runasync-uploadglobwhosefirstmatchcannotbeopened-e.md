---
id: AF-0733
title: RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch asserts exit 25, not 26
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0733 - RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch asserts exit 25, not 26

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`: RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch asserts exit 25, not 26. Reappeared; previously AF-0648.

## Evidence

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`

The name states exit 26; line 430 asserts Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode) (25), and the pinned -w output is nosuchfile 25. No assertion of 26 or ReadError.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch' -Context 0,6
```

- Expected: The body asserts exit 26, or the name states 25.
- Actual: Line 430: Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode); - exit 25.

## Re-audits

## Log

- 2026-11-13: filed proposed.
