---
id: BL-4828
title: Fix AF-0646: Cgr170 test named Exit26 asserts exit 25
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4828 — Fix AF-0646: Cgr170 test named Exit26 asserts exit 25

## Goal

The defect the audit office reported as AF-0646 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0646 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0646-cgr170-test-named-exit26-asserts-exit-25.md`.

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:46`

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:46`

The name promises exit 26 (read error), but the body asserts Assert.AreEqual(25, exitCode) and stdout '0 25 000'. The name states one exit code and the test checks another.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/Cgr170Tests.cs -Pattern 'ReportsEveryTransferWithExit26AndUploadsNothing' -Context 0,8
```

- Expected: The name and the asserted exit code agree.
- Actual: Name says Exit26; body: Assert.AreEqual(25, exitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-04: No code change needed. `Cgr170Tests.cs:46` already asserts `Assert.AreEqual(26, exitCode)` and stdout "0 26 000 / 1 26 000" since 27116a06b (BL-3968), so the name and the asserted exit code agree; the finding was most likely recorded against a seeded or older audit tree. Ran the reproduction (Select-String shows exit 26 in the body), `dotnet build` (0 errors) and the fast tests (114 projects passed, 0 failed).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Cgr170 Exit26 test name and asserted exit code agree (26); reproduction gives expected result
