---
id: BL-5583
title: Fix AF-0895: RunAsync_FormFileThatCannotBeOpened_Exits26WithoutConnecting asserts exit 25
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests/CurlCommandRunnerFormTests.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5583 — Fix AF-0895: RunAsync_FormFileThatCannotBeOpened_Exits26WithoutConnecting asserts exit 25

## Goal

The defect the audit office reported as AF-0895 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0895 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Console.UnitTests/CurlCommandRunnerFormTests.cs:151`

Location: `Curl.Console.UnitTests/CurlCommandRunnerFormTests.cs:151`

The name says Exits26. The body asserts Assert.AreEqual(25, exitCode); and the standard error 'curl: (25) ...'. Nothing checks 26.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerFormTests.cs -Pattern 'Exits26WithoutConnecting|AreEqual\(25, exitCode'
```

- Expected: The name and the asserted exit code agree.
- Actual: Line 151 names Exits26; line 164 asserts Assert.AreEqual(25, exitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Already fixed in the tree: the test asserts 26 (exit code and stderr), the reproduction finds no `AreEqual(25, exitCode`; the test passes. No code change.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
