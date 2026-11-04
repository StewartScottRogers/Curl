---
id: AF-0595
title: Surviving mutant: the NSEC3 closest-encloser search can start above the name's own label count without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:ClosestEncloser-minus1:surviving-mutant
reproduction: mutation Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:116:-1
task: BL-4759
tasks: BL-4759
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1615.md
---
# AF-0595 - Surviving mutant: the NSEC3 closest-encloser search can start above the name's own label count without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:116`: Surviving mutant: the NSEC3 closest-encloser search can start above the name's own label count without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:116`

Drawn library. for (int labels = name.LabelCount - 1; ...) -> LabelCount + 1 survived: the search then tries 'ancestors' with more labels than the name, so the name itself can be taken as its own closest encloser (RFC 5155 section 8.3 requires a proper ancestor), changing the opt-out insecure-delegation decision.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:116:-1 -Member ClosestEncloser -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the -Site command: DenialOfExistence.cs:116 -1 resolved to line 116, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
