---
id: BL-4455
title: Fix AF-0520: Lib1965_OnARefusedScheme_WritesRejected passes on empty output
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-11-01
completed: 2026-11-01
---
# BL-4455 — Fix AF-0520: Lib1965_OnARefusedScheme_WritesRejected passes on empty output

## Goal

The defect the audit office reported as AF-0520 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0520 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0520-lib1965-onarefusedscheme-writesrejected-passes-on.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs:97`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs:97`

The only output check is Assert.IsTrue(run.OutputText.Split('\n', RemoveEmptyEntries).All(line => line.EndsWith(" REJECTED"))). All() is true for no lines, so a Lib1965 that wrote nothing would pass. The neighbouring test asserts Assert.HasCount(Lib1965.Schemes.Count + 1, lines).

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1964To1977Tests.cs -Pattern 'Lib1965_OnARefusedScheme_WritesRejected' -Context 0,8
```

- Expected: A line-count assertion beside the All(...) check.
- Actual: Only Assert.AreEqual(0, result.ExitCode) and Assert.IsTrue(...All(line => line.EndsWith(" REJECTED", ...))).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Assert.HasCount(Lib1965.Schemes.Count, lines)` before the All(...) check, so empty output now fails the test. The reproduction now shows the line-count assertion. dotnet build is clean and the fast tests are green (no Failed! line in the run).

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-01: Doing -> Done. Lib1965_OnARefusedScheme_WritesRejected asserts the line count, so empty output fails
