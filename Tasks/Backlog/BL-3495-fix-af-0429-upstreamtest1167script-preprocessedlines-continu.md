---
id: BL-3495
title: Fix AF-0429: UpstreamTest1167Script.PreprocessedLines' continuesDirective || line.StartsWith('#') can become && with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-25
completed:
---
# BL-3495 — Fix AF-0429: UpstreamTest1167Script.PreprocessedLines' continuesDirective || line.StartsWith('#') can become && with no test failing

## Goal

The defect the audit office reported as AF-0429 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0429 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0429-upstreamtest1167script-preprocessedlines-continues.md`.

Location: `Curl.Conformance.UnitLibrary/UpstreamTest1167Script.cs:104`

Location: `Curl.Conformance.UnitLibrary/UpstreamTest1167Script.cs:104`

Sampled mutant (seed 0) survived: 'bool directive = continuesDirective || line.TrimStart().StartsWith('#');' -> '... && ...'. continuesDirective starts false, so with && no line is ever a directive: every #define/#include line and its continuation lines are yielded as code instead of dropped, the opposite of the preprocessor view the method's comment describes. The test1167 port's verdict over curl's headers then rests on lines it should not see, and no test feeds a header with a directive through this path.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/UpstreamTest1167Script.cs:104:|| -Member PreprocessedLines -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
