---
id: BL-5584
title: Fix AF-0896: RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch asserts UploadFailed (25)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5584 — Fix AF-0896: RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch asserts UploadFailed (25)

## Goal

The defect the audit office reported as AF-0896 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0896 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`

The name says Exits26. The body asserts Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode); (25) and the stdout 'nosuchfile 25'. Nothing checks 26.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'Exits26|CurlExitCode.UploadFailed, exitCode'
```

- Expected: The name and the asserted exit code agree.
- Actual: Line 425 names Exits26; line 430 asserts Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
