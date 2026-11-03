---
id: BL-4765
title: Fix AF-0601: Surviving mutant: lib591 can refuse an accept timeout of 0 without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4765 — Fix AF-0601: Surviving mutant: lib591 can refuse an accept timeout of 0 without a test failing

## Goal

The defect the audit office reported as AF-0601 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0601 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0601-surviving-mutant-lib591-can-refuse-an-accept-timeo.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib591.cs:66`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib591.cs:66`

seconds is >= 0 -> > 0 survived: an argument of '0' is then refused as not a timeout, a decision the port makes from its command line.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib591.cs:66:>= -Member TryParseAcceptTimeout -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
