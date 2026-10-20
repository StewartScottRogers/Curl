---
id: AF-0274
title: Lib552's debug trace noHex: true can become false with no test failing, changing its stderr dump
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:50:true
task: BL-2801
tasks: BL-2801
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed: 2026-10-19
closed-how: mechanical
closed-by: 2026-10-19_2134.md
---
# AF-0274 - Lib552's debug trace noHex: true can become false with no test failing, changing its stderr dump

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:50`: Lib552's debug trace noHex: true can become false with no test failing, changing its stderr dump. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:50`

Mutant new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: false, noHex: true) -> noHex: false survived (sampled run, seed 0). The program's stderr trace switches to a hex dump. That is output bytes of the port the upstream case runner runs, and no test pins them.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:50:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-10-19 | 2026-10-19_2134.md | reproduces: no | Ran the -Site command: Lib552.cs:50:true in TestAsync; outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
- 2026-10-19: accepted -> closed. Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
