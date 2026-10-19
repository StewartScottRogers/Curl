---
id: AF-0275
title: Lib1537.LengthToNul's nul < 0 can become <= 0 with no test failing, so bytes starting with NUL count as their full length
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1537.cs:LengthToNul-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1537.cs:84:<
task: BL-2802
tasks: BL-2802
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0275 - Lib1537.LengthToNul's nul < 0 can become <= 0 with no test failing, so bytes starting with NUL count as their full length

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1537.cs:84`: Lib1537.LengthToNul's nul < 0 can become <= 0 with no test failing, so bytes starting with NUL count as their full length. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1537.cs:84`

Mutant return nul < 0 ? raw.Length : nul -> nul <= 0 survived (sampled run, seed 0). LengthToNul is documented as strlen of decoded bytes. With the mutant, decoded bytes whose first byte is NUL report raw.Length instead of 0, which changes the length lib1537 prints. No test decodes bytes starting with NUL.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1537.cs:84:< -Member LengthToNul -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
