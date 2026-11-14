---
id: AF-0721
title: Surviving mutant: lib530's debug trace can drop its hex dumps (noHex true)
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:TransferAsync-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:144:false
task: BL-5080
tasks: BL-5080
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0721 - Surviving mutant: lib530's debug trace can drop its hex dumps (noHex true)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:144`: Surviving mutant: lib530's debug trace can drop its hex dumps (noHex true). Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:144`

Mutant 'noHex: false' -> 'noHex: true' survived. This changes the bytes lib530 writes to standard error, which upstream test530 compares. No unit test pins lib530's trace format (the same class of gap as AF-0691 for lib753).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib530Run.cs:144:false -Member TransferAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-13 | 2026-11-13_2321.md | not re-audited | Ran the -Site command: outcome killed, but line 144 now reads new LibTestDebugTrace(..., traceTime: false, noHex: false), and the tool mutated the first false (traceTime), not the noHex value the finding is about. Not re-audited.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
