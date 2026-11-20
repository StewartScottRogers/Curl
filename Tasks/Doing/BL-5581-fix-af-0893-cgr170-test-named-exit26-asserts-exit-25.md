---
id: BL-5581
title: Fix AF-0893: Cgr170 test named Exit26 asserts exit 25
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/Cgr170Tests.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5581 — Fix AF-0893: Cgr170 test named Exit26 asserts exit 25

## Goal

The defect the audit office reported as AF-0893 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0893 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:46`

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:46`

The name says ReportsEveryTransferWithExit26. The body asserts Assert.AreEqual(25, exitCode); and Assert.AreEqual("0 25 000\n1 25 000\n", StandardOutput); with standard error 'curl: (25) Upload failed'. Nothing checks 26.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/Cgr170Tests.cs -Pattern 'Exit26|AreEqual\(25'
```

- Expected: The name and the asserted exit code agree.
- Actual: Line 46 names Exit26; line 53 asserts Assert.AreEqual(25, exitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
