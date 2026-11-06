---
id: BL-4885
title: Fix AF-0691: Surviving mutant: lib753's debug trace can write hex dumps (noHex false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
---
# BL-4885 — Fix AF-0691: Surviving mutant: lib753's debug trace can write hex dumps (noHex false)

## Goal

The defect the audit office reported as AF-0691 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0691 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0691-surviving-mutant-lib753-s-debug-trace-can-write-he.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41`

Invoke-MutationTest (Curl.Conformance.LibTest.UnitLibrary, -MaxMutants 40, -Seed 982898) changed '... new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true));' to '... noHex: false));' and every LibTest.UnitTests test passed (survived). I confirmed this by hand in a scratch worktree: 1205/1205 passed. lib753's stderr trace would gain hex dump columns unnoticed, unlike lib500/1515/1522/1540/2700, whose noHex mutants are now killed. Note: the line has two 'true' sites. The -Site form takes the first (traceTime), so a site rerun mutates traceTime, not this noHex mutant.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-06: Created.
