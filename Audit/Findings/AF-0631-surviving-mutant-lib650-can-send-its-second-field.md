---
id: AF-0631
title: Surviving mutant: lib650 can send its second field's name one byte longer (NameLength +1 instead of -1), and no test fails
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:BuildForm-minus1:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:96:-1
task: none
tasks:
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0631 - Surviving mutant: lib650 can send its second field's name one byte longer (NameLength +1 instead of -1), and no test fails

## Summary

High finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:96`: Surviving mutant: lib650 can send its second field's name one byte longer (NameLength +1 instead of -1), and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:96`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'new(CurlFormOption.NameLength, (long)TestName.Length - 1)' -> '+ 1'. Upstream test650 sends the second part under the name cut by one byte (CURLFORM_NAMELENGTH). The mutant changes the multipart request bytes the port builds, and the LibTest tests never compare that part's name.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:96:-1 -Member BuildForm -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
