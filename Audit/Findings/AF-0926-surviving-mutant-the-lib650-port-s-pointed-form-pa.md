---
id: AF-0926
title: Surviving mutant: the lib650 port's pointed form part can be sent two bytes longer
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:BuildForm-minus1:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:92:-1
task: none
tasks:
found: 2026-11-21
found-at: 1f330a1f28a1086717db7833947030e8fdc0bf9d
scorecard: 2026-11-21_0114.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0926 - Surviving mutant: the lib650 port's pointed form part can be sent two bytes longer

## Summary

High finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:92`: Surviving mutant: the lib650 port's pointed form part can be sent two bytes longer. Reappeared; previously AF-0631.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:92`

Mutant 'new(CurlFormOption.ContentsLength, (long)TestData.Length - 1)' -> '(long)TestData.Length + 1' survived (seed 276683 sample). Upstream lib650 passes strlen(testdata) - 1, so the part's contents length, and with it the multipart request bytes the port sends, change. No LibTest unit test checks the length or bytes of the second form part that BuildForm builds.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:92:-1 -Member BuildForm -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
