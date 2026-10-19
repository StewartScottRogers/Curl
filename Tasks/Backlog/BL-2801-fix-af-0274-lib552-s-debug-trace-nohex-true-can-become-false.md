---
id: BL-2801
title: Fix AF-0274: Lib552's debug trace noHex: true can become false with no test failing, changing its stderr dump
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-10-19
completed:
---
# BL-2801 — Fix AF-0274: Lib552's debug trace noHex: true can become false with no test failing, changing its stderr dump

## Goal

The defect the audit office reported as AF-0274 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0274 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0274-lib552-s-debug-trace-nohex-true-can-become-false-w.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:50`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:50`

Mutant new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: false, noHex: true) -> noHex: false survived (sampled run, seed 0). The program's stderr trace switches to a hex dump. That is output bytes of the port the upstream case runner runs, and no test pins them.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:50:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-19: Created.
