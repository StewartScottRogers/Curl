---
id: BL-4694
title: Fix AF-0546: RefusesAsNotProperNumerical checks only that some refusal exists, not its text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4694 — Fix AF-0546: RefusesAsNotProperNumerical checks only that some refusal exists, not its text

## Goal

The defect the audit office reported as AF-0546 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0546 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0546-refusesasnotpropernumerical-checks-only-that-some.md`.

Location: `Curl.Cli.UnitTests/CommandLineNumberTests.cs:175`

Location: `Curl.Cli.UnitTests/CommandLineNumberTests.cs:175`

The only check of the refusal is 'Assert.IsNotNull(refusal);'; the other assertions check number == 0. The name promises the 'not proper numerical' refusal, yet any refusal message or exit code passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitTests/CommandLineNumberTests.cs -Pattern 'RefusesAsNotProperNumerical' -Context 0,7
```

- Expected: The test asserts the refusal's standard-error line and exit code.
- Actual: Only Assert.IsNotNull(refusal); then Assert.AreEqual(0L, number);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Test now asserts refusal text and exit code
