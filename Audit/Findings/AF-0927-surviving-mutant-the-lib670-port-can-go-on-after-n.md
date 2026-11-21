---
id: AF-0927
title: Surviving mutant: the lib670 port can go on after naming its mime part fails
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:BindMime-false:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:102:false
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
# AF-0927 - Surviving mutant: the lib670 port can go on after naming its mime part fails

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:102`: Surviving mutant: the lib670 port can go on after naming its mime part fails.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:102`

Mutant 'return false;' -> 'return true;' after 'if (steps.Result != CurlCode.Ok)' on part.SetName("field") survived (seed 276683 sample). The port writes 'Something went wrong when building the mime structure' and then carries on to bind and perform instead of stopping, as upstream lib670 does. No test makes SetName fail.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib670.cs:102:false -Member BindMime -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
