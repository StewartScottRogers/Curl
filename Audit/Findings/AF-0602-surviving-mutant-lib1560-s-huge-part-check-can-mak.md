---
id: AF-0602
title: Surviving mutant: lib1560's huge-part check can make every part but one huge without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs:Huge-eq:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs:108:==
task: BL-4766
tasks: BL-4766
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1615.md
---
# AF-0602 - Surviving mutant: lib1560's huge-part check can make every part but one huge without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs:108`: Surviving mutant: lib1560's huge-part check can make every part but one huge without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs:108`

piece == i ? BigPart : "c" -> piece != i survived: the check then builds URLs with six huge parts instead of one, a different upstream check whose error count reaches lib1560's output; no test pins Huge's result.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.Handles.cs:108:== -Member Huge -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the -Site command: Lib1560Checks.Handles.cs:108 == resolved to line 108, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
