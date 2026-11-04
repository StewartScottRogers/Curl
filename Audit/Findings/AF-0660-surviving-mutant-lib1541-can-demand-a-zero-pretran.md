---
id: AF-0660
title: Surviving mutant: lib1541 can demand a zero PRETRANSFER_TIME_T at the first header
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:CheckFirstHeader-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:91:false
task: BL-4842
tasks: BL-4842
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0660 - Surviving mutant: lib1541 can demand a zero PRETRANSFER_TIME_T at the first header

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:91`: Surviving mutant: lib1541 can demand a zero PRETRANSFER_TIME_T at the first header.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:91`

Mutant 'mustBeZero: false' -> 'mustBeZero: true' for CURLINFO_PRETRANSFER_TIME_T survived. The port would then print a failure line whenever the time is nonzero. No test checks that line.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:91:false -Member CheckFirstHeader -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
