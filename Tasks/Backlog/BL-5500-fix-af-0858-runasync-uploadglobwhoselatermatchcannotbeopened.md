---
id: BL-5500
title: Fix AF-0858: RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun asserts UploadFailed (25), not 26
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5500 — Fix AF-0858: RunAsync_UploadGlobWhoseLaterMatchCannotBeOpenedAfterASuccess_Exits26AndStopsTheRun asserts UploadFailed (25), not 26

## Goal

The defect the audit office reported as AF-0858 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0858 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:449`

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:449`

The name says Exits26. Line 455 is Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode); and line 473 expects stdout 'http://h/g/nosuchfile 25'. No assertion of 26.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'AfterASuccess_Exits26|CurlExitCode.UploadFailed, exitCode'
```

- Expected: The name and the asserted exit code agree.
- Actual: CurlCommandRunnerUploadTests.cs:449 ..._Exits26AndStopsTheRun and :455 Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-18: Created.
