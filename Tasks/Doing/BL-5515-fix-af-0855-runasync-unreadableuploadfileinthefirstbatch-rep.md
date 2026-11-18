---
id: BL-5515
title: Fix AF-0855: RunAsync_UnreadableUploadFileInTheFirstBatch_ReportsEveryTransferWithExit26AndUploadsNothing asserts exit 25, not the 26 its name states
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr170Tests.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5515 — Fix AF-0855: RunAsync_UnreadableUploadFileInTheFirstBatch_ReportsEveryTransferWithExit26AndUploadsNothing asserts exit 25, not the 26 its name states

## Goal

The defect the audit office reported as AF-0855 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0855 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:46`

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:46`

The name promises exit 26 (CURLE_READ_ERROR). The body asserts the other number: line 53 Assert.AreEqual(25, exitCode); line 54 Assert.AreEqual("0 25 000\n1 25 000\n", StandardOutput); and stderr 'curl: (25) Upload failed'. No assertion mentions 26 or CurlExitCode.ReadError. Find-WeakTests name-lies candidate, filed under Quality.md step 0's rule that an assertion of a different number is the finding.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/Cgr170Tests.cs -Pattern 'Exit26AndUploadsNothing|Assert.AreEqual\(25, exitCode\)'
```

- Expected: The name and the asserted exit code agree (both 26, or the name says 25).
- Actual: Cgr170Tests.cs:46 ...ReportsEveryTransferWithExit26AndUploadsNothing() and Cgr170Tests.cs:53 Assert.AreEqual(25, exitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Already fixed by an earlier commit (27116a06b4, BL-3968): the test asserts 26 and the reproduction finds no `Assert.AreEqual(25, exitCode)`. No code changed; the unchanged test file passed the fast tests in that commit.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
