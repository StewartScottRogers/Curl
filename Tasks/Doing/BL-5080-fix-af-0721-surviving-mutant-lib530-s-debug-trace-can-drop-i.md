---
id: BL-5080
title: Fix AF-0721: Surviving mutant: lib530's debug trace can drop its hex dumps (noHex true)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-5080 — Fix AF-0721: Surviving mutant: lib530's debug trace can drop its hex dumps (noHex true)

## Goal

The defect the audit office reported as AF-0721 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0721 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0721-surviving-mutant-lib530-s-debug-trace-can-drop-its.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:144`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:144`

Mutant 'noHex: false' -> 'noHex: true' survived. This changes the bytes lib530 writes to standard error, which upstream test530 compares. No unit test pins lib530's trace format (the same class of gap as AF-0691 for lib753).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:144:false -Member TransferAsync -ExcludeBaselineFailures -TimeoutSeconds 600
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
