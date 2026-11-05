---
id: AF-0648
title: Upload glob test named Exits26 asserts exit 25
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch:name-lies
reproduction: none
task: BL-4830
tasks: BL-4830
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0648 - Upload glob test named Exits26 asserts exit 25

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`: Upload glob test named Exits26 asserts exit 25.

## Evidence

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`

The name says Exits26. The body asserts CurlExitCode.UploadFailed (25) and stdout '<url>nosuchfile 25'.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'FirstMatchCannotBeOpened_Exits26' -Context 0,6
```

- Expected: The name and the asserted exit code agree.
- Actual: Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode);

## Re-audits

- 2026-11-04 | 2026-11-04_1442.md | reproduces: no | Ran the Select-String: CurlCommandRunnerUploadTests.cs:430 Assert.AreEqual((int)CurlExitCode.ReadError, exitCode); and CurlExitCode.ReadError = 26 (CurlExitCode.cs:118), matching Exits26.

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
