---
id: AF-0897
title: RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun asserts UploadFailed (25)
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
key: quality:Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun:name-lies
reproduction: none
task: BL-5585
tasks: BL-5585
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed: 2026-11-20
closed-how: reliable-reaudit
closed-by: 2026-11-20_0456.md
---
# AF-0897 - RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun asserts UploadFailed (25)

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:449`: RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun asserts UploadFailed (25). Reappeared; previously AF-0649.

## Evidence

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:449`

The name says Exits26. The body asserts Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode); (25) and the stdout 'http://h/g/nosuchfile 25'. Nothing checks 26.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'Exits26|CurlExitCode.UploadFailed, exitCode'
```

- Expected: The name and the asserted exit code agree.
- Actual: Line 449 names Exits26; line 455 asserts Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode);

## Re-audits

- 2026-11-20 | 2026-11-20_0456.md | reproduces: no | Ran the Select-String: no 'CurlExitCode.UploadFailed, exitCode' match. RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun asserts Assert.AreEqual((int)CurlExitCode.ReadError, exitCode) (26).

## Log

- 2026-11-19: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
