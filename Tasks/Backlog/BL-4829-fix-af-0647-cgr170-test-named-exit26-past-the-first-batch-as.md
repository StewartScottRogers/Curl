---
id: BL-4829
title: Fix AF-0647: Cgr170 test named Exit26 (past the first batch) asserts exit 25
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-4829 — Fix AF-0647: Cgr170 test named Exit26 (past the first batch) asserts exit 25

## Goal

The defect the audit office reported as AF-0647 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0647 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0647-cgr170-test-named-exit26-past-the-first-batch-asse.md`.

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:89`

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:89`

The name promises exit 26. The body asserts Assert.AreEqual(25, exitCode) and stdout '0 7 / 1 7 / 2 25'.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/Cgr170Tests.cs -Pattern 'ReportsExit26WhateverFailedBefore' -Context 0,12
```

- Expected: The name and the asserted exit code agree.
- Actual: Name says Exit26; body: Assert.AreEqual(25, exitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
