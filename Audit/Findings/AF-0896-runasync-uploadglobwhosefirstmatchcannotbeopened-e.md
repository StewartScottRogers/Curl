---
id: AF-0896
title: RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch asserts UploadFailed (25)
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
key: quality:Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch:name-lies
reproduction: none
task: BL-5584
tasks: BL-5584
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed: 2026-11-20
closed-how: reliable-reaudit
closed-by: 2026-11-20_0456.md
---
# AF-0896 - RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch asserts UploadFailed (25)

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`: RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch asserts UploadFailed (25). Reappeared; previously AF-0648.

## Evidence

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`

The name says Exits26. The body asserts Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode); (25) and the stdout 'nosuchfile 25'. Nothing checks 26.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'Exits26|CurlExitCode.UploadFailed, exitCode'
```

- Expected: The name and the asserted exit code agree.
- Actual: Line 425 names Exits26; line 430 asserts Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode);

## Re-audits

- 2026-11-20 | 2026-11-20_0456.md | reproduces: no | Ran the Select-String: no 'CurlExitCode.UploadFailed, exitCode' match. RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch asserts Assert.AreEqual((int)CurlExitCode.ReadError, exitCode) (26) and "...nosuchfile 26\n".

## Log

- 2026-11-19: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
