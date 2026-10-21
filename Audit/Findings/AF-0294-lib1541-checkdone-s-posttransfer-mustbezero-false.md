---
id: AF-0294
title: Lib1541.CheckDone's POSTTRANSFER mustBeZero: false can become true with no test failing, printing WRONG on stdout
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:CheckDone-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:106:false
task: none
tasks:
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0294 - Lib1541.CheckDone's POSTTRANSFER mustBeZero: false can become true with no test failing, printing WRONG on stdout

## Summary

High finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:106`: Lib1541.CheckDone's POSTTRANSFER mustBeZero: false can become true with no test failing, printing WRONG on stdout.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:106`

Sampled mutant (seed 0) survived: CheckTime(easy, output, CurlInfo.PosttransferTimeT, "CURLINFO_POSTTRANSFER_TIME_T", Where, mustBeZero: false) => mustBeZero: true. CheckTime (line 139) prints "<name> on done is OK" when time > 0, but with mustBeZero only when time == 0. So a normal transfer would write "CURLINFO_POSTTRANSFER_TIME_T on done is WRONG: N" to the lib1541 program's stdout, which upstream test1541 compares. No Curl.Conformance.LibTest unit test pins CheckDone's output for the post-transfer time.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1541.cs:106:false -Member CheckDone -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
