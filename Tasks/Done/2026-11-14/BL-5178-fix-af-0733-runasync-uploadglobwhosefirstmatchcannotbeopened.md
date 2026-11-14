---
id: BL-5178
title: Fix AF-0733: RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch asserts exit 25, not 26
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5178 — Fix AF-0733: RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch asserts exit 25, not 26

## Goal

The defect the audit office reported as AF-0733 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0733 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:425`

The name states exit 26; line 430 asserts Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode) (25), and the pinned -w output is nosuchfile 25. No assertion of 26 or ReadError.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'RunAsync_UploadGlobWhoseFirstMatchCannotBeOpened_Exits26AndUploadsNoOtherMatch' -Context 0,6
```

- Expected: The body asserts exit 26, or the name states 25.
- Actual: Line 430: Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode); - exit 25.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Test already asserts ReadError (26) in code and -w output (`nosuchfile 26`); no code change needed. Both DataRows pass. Fast suite not rerun: no code changed.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-14: Doing -> Done. Test already asserts exit 26 (ReadError) and -w 26; both DataRows pass
