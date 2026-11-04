---
id: AF-0659
title: Surviving mutant: lib670 can go on after curl_formadd refuses its field
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:BindForm-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:129:false
task: none
tasks:
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0659 - Surviving mutant: lib670 can go on after curl_formadd refuses its field

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:129`: Surviving mutant: lib670 can go on after curl_formadd refuses its field.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:129`

Mutant 'return false;' -> 'return true;' after a refused curl_formadd survived. The port then performs with a broken form instead of stopping, which changes its output. No test fails.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:129:false -Member BindForm -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
