---
id: AF-0662
title: Surviving mutant: lib1522's debug trace can write hex dumps (noHex false)
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63:true
task: BL-4844
tasks: BL-4844
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0662 - Surviving mutant: lib1522's debug trace can write hex dumps (noHex false)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63`: Surviving mutant: lib1522's debug trace can write hex dumps (noHex false).

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63`

Mutant 'noHex: true' -> 'noHex: false' on its LibTestDebugTrace survived. The port's stderr trace would then carry hex dumps that upstream lib1522 does not write. No test pins that trace.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1522.cs:63:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-04 | 2026-11-04_1442.md | not re-audited | Ran the -Site command: outcome killed, but the mutant it applied was 'traceTime: false, noHex: true'. The tool takes the line's first 'true' site, so it never mutated the noHex flag this finding names. The run does not tell whether the noHex mutant is killed. This audit's sample found the same noHex mutant surviving in Lib1540, Lib500 and Lib1515.
- 2026-11-05 | 2026-11-05_0819.md | not re-audited | Ran the -Site command: outcome killed, but the site resolved line 63's first 'true', which is now traceTime ('traceTime: true' -> 'traceTime: false'), not the noHex argument the finding names. The noHex mutant was not run, so the result does not tell.

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
