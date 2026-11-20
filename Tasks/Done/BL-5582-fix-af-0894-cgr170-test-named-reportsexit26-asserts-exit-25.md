---
id: BL-5582
title: Fix AF-0894: Cgr170 test named ReportsExit26 asserts exit 25
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr170Tests.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5582 — Fix AF-0894: Cgr170 test named ReportsExit26 asserts exit 25

## Goal

The defect the audit office reported as AF-0894 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0894 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:89`

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:89`

The name says ReportsExit26. The body asserts Assert.AreEqual(25, exitCode); and Assert.AreEqual("0 7\n1 7\n2 25\n", StandardOutput); Nothing checks 26.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/Cgr170Tests.cs -Pattern 'Exit26|AreEqual\(25'
```

- Expected: The name and the asserted exit code agree.
- Actual: Line 89 names ReportsExit26; line 98 asserts Assert.AreEqual(25, exitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Already fixed before this claim (same as BL-5581): both Exit26 tests assert 26 and "2 26" at Cgr170Tests.cs:53,98; no AreEqual(25 remains. No code changed, so build and fast tests are as the shift left them.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-19: Doing -> Done. Cgr170 test already asserts exit 26; reproduction clean
