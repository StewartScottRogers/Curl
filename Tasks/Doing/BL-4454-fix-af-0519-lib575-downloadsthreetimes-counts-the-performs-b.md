---
id: BL-4454
title: Fix AF-0519: Lib575_DownloadsThreeTimes counts the performs but never asserts the count
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-11-01
completed:
---
# BL-4454 — Fix AF-0519: Lib575_DownloadsThreeTimes counts the performs but never asserts the count

## Goal

The defect the audit office reported as AF-0519 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0519 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0519-lib575-downloadsthreetimes-counts-the-performs-but.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib567To576Tests.cs:417`

Location: `Curl.Conformance.LibTest.UnitTests/Lib567To576Tests.cs:417`

The body increments 'performs' in the fake engine's Performing callback but asserts only result.ExitCode, ProgramName and a null-argument throw. Nothing checks that three downloads happened. The Lib574 test just above it asserts Assert.AreEqual(performCount, performs). A Lib575 that performed once, or stopped early with the same code, would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib567To576Tests.cs -Pattern 'Lib575_DownloadsThreeTimes' -Context 0,12
```

- Expected: An assertion on performs (3 on the success row).
- Actual: Asserts only Assert.AreEqual(exitCode, result.ExitCode), the program name and ThrowsExactly<ArgumentNullException>; performs is never read.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
