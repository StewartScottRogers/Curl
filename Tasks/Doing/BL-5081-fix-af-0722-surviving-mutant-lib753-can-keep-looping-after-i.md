---
id: BL-5081
title: Fix AF-0722: Surviving mutant: lib753 can keep looping after its test timeout aborts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-5081 — Fix AF-0722: Surviving mutant: lib753 can keep looping after its test timeout aborts

## Goal

The defect the audit office reported as AF-0722 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0722 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0722-surviving-mutant-lib753-can-keep-looping-after-its.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:144`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:144`

Mutant 'return (false, 0);' -> 'return (true, 0);' survived. That return is taken when steps.AbortOnTestTimeout(clock) refuses. With the mutant the loop goes on after the 'ABORTING TEST' line, so the hang guard is lost and no test drives lib753 past its timeout.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:144:false -Member StartAndLoopAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
