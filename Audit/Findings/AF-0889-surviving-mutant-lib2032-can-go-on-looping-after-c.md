---
id: AF-0889
title: Surviving mutant: lib2032 can go on looping after curl_multi_perform fails
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib2032.cs:AfterPerform-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib2032.cs:193:false
task: none
tasks:
found: 2026-11-19
found-at: 6e68cb60b626d421113f6d7d7f35c2a46bbc3b46
scorecard: 2026-11-19_0602.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0889 - Surviving mutant: lib2032 can go on looping after curl_multi_perform fails

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib2032.cs:193`: Surviving mutant: lib2032 can go on looping after curl_multi_perform fails.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2032.cs:193`

Mutant return (false, running); -> return (true, running); survived all Curl.Conformance.LibTest.UnitTests (seed 446316). It sits in the turn that runs steps.MultiPerformAsync: when the perform step does not go on, the program should stop, but the mutant carries on into the next turn and writes its running/state line. No test makes lib2032's multi perform fail.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib2032.cs:193:false -Member AfterPerform -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-19: filed proposed.
