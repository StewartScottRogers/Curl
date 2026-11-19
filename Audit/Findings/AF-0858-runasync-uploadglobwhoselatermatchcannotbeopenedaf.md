---
id: AF-0858
title: RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun asserts UploadFailed (25), not 26
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
key: quality:Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun:name-lies
reproduction: none
task: BL-5500
tasks: BL-5500
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed: 2026-11-19
closed-how: reliable-reaudit
closed-by: 2026-11-19_0134.md
---
# AF-0858 - RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun asserts UploadFailed (25), not 26

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:449`: RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun asserts UploadFailed (25), not 26. Reappeared; previously AF-0649. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:449`

The name says Exits26. Line 455 is Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode); and line 473 expects stdout 'http://h/g/nosuchfile 25'. No assertion of 26.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'AfterASuccess_Exits26|CurlExitCode.UploadFailed, exitCode'
```

- Expected: The name and the asserted exit code agree.
- Actual: CurlCommandRunnerUploadTests.cs:449 ..._Exits26AndStopsTheRun and :455 Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode);

## Re-audits

- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Only the test name matched (line 449); there is no 'CurlExitCode.UploadFailed, exitCode'. The test asserts (int)CurlExitCode.ReadError (26) at line 454.

## Log

- 2026-11-18: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
