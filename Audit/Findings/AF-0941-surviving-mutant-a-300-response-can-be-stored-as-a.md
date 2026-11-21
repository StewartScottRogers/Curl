---
id: AF-0941
title: Surviving mutant: a 300 response can be stored as a compression dictionary
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs:IsSuccessfulResponse-lt:surviving-mutant
reproduction: mutation Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs:147:<
task: BL-5700
tasks: BL-5700
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0941 - Surviving mutant: a 300 response can be stored as a compression dictionary

## Summary

Medium finding from the quality auditor at `Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs:147`: Surviving mutant: a 300 response can be stored as a compression dictionary. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs:147`

Drawn library, seed 812510: Report.ResponseCode: >= 200 and < 300 mutated to <= 300 survived; a 300 Multiple Choices response carrying Use-As-Dictionary would be stored and later advertised. No test sends a 300.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CompressionDictionary.UnitLibrary/CompressionDictionaryTransferHook.cs:147:< -Member IsSuccessfulResponse -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
