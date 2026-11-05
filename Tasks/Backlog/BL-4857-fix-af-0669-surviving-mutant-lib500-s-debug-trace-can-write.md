---
id: BL-4857
title: Fix AF-0669: Surviving mutant: lib500's debug trace can write hex dumps (noHex false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4857 — Fix AF-0669: Surviving mutant: lib500's debug trace can write hex dumps (noHex false)

## Goal

The defect the audit office reported as AF-0669 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0669 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0669-surviving-mutant-lib500-s-debug-trace-can-write-he.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:50`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:50`

Sampled mutant 'noHex: true' -> 'noHex: false' in 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' survived (sample -Seed 413592). Upstream lib500 sets nohex; the trace lines it writes change and no test pins them. Same caveat as Lib1540: -Site ...:50:true mutates the first 'true' (traceTime) on the line, not this noHex site.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:50:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (sampled noHex mutant)

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
