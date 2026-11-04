---
id: AF-0649
title: Upload glob (later match) test named Exits26 asserts exit 25
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0649 - Upload glob (later match) test named Exits26 asserts exit 25

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:449`: Upload glob (later match) test named Exits26 asserts exit 25.

## Evidence

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:449`

The name says Exits26. The body asserts CurlExitCode.UploadFailed (25) and stdout 'http://h/g/nosuchfile 25'.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'LaterMatchCannotBeOpenedAfterASuccess_Exits26' -Context 0,6
```

- Expected: The name and the asserted exit code agree.
- Actual: Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode);

## Re-audits

## Log

- 2026-11-04: filed proposed.
