---
id: AF-0564
title: Surviving mutant: a lib1560 scheme-guess table row can change without a test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.Handles.cs:SchemeGuesses-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.Handles.cs:22:false
task: none
tasks:
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0564 - Surviving mutant: a lib1560 scheme-guess table row can change without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.Handles.cs:22`: Surviving mutant: a lib1560 scheme-guess table row can change without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.Handles.cs:22`

Mutant new("smtp.example.com:99/moo", false, ...) -> new("smtp.example.com:99/moo", true, ...) survived. The row is upstream lib1560's case and Lib1560.RunAsync checks every row (checks.SchemeGuess(Lib1560Tables.SchemeGuesses)), but no unit test pins the rows, so a wrong row changes what the port checks and what it prints unnoticed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1560Tables.Handles.cs:22:false -Member SchemeGuesses -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
