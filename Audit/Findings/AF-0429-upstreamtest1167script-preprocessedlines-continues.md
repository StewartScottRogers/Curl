---
id: AF-0429
title: UpstreamTest1167Script.PreprocessedLines' continuesDirective || line.StartsWith('#') can become && with no test failing
auditor: quality
severity: Medium
status: accepted
reason: Stewart accepted it on 2026-10-25.
key: quality:Curl.Conformance.UnitLibrary/UpstreamTest1167Script.cs:PreprocessedLines-or:surviving-mutant
reproduction: mutation Curl.Conformance.UnitLibrary/UpstreamTest1167Script.cs:104:||
task: BL-3495
tasks: BL-3495
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0429 - UpstreamTest1167Script.PreprocessedLines' continuesDirective || line.StartsWith('#') can become && with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitLibrary/UpstreamTest1167Script.cs:104`: UpstreamTest1167Script.PreprocessedLines' continuesDirective || line.StartsWith('#') can become && with no test failing.

## Evidence

Location: `Curl.Conformance.UnitLibrary/UpstreamTest1167Script.cs:104`

Sampled mutant (seed 0) survived: 'bool directive = continuesDirective || line.TrimStart().StartsWith('#');' -> '... && ...'. continuesDirective starts false, so with && no line is ever a directive: every #define/#include line and its continuation lines are yielded as code instead of dropped, the opposite of the preprocessor view the method's comment describes. The test1167 port's verdict over curl's headers then rests on lines it should not see, and no test feeds a header with a directive through this path.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/UpstreamTest1167Script.cs:104:|| -Member PreprocessedLines -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-24: filed proposed.
- 2026-10-25: proposed -> accepted.
