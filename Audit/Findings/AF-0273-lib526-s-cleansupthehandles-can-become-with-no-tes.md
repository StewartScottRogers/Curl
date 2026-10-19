---
id: AF-0273
title: Lib526's CleansUpTheHandles || can become && with no test failing, so cases 526, 528 and 532 skip their clean-up
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:CleansUpTheHandles-or:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147:||
task: none
tasks:
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0273 - Lib526's CleansUpTheHandles || can become && with no test failing, so cases 526, 528 and 532 skip their clean-up

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`: Lib526's CleansUpTheHandles || can become && with no test failing, so cases 526, 528 and 532 skip their clean-up. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`

Mutant testNumber is 526 or 528 or 532 || (testNumber == 527 && steps.Result != CurlCode.Ok) -> && survived (sampled run, seed 0). With && the expression is always false, so no case cleans up its handles the way upstream lib526.c does for its case numbers. This is a decision the port makes, and no test pins it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147:|| -Member CleansUpTheHandles -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
