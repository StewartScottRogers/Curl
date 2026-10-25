---
id: AF-0437
title: Lib1515.TestAsync's i < RequestCount can become <= with no test failing, adding a DNS-timeout wait after the last request
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:TestAsync-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:74:<
task: none
tasks:
found: 2026-10-25
found-at: 03fe150efe7a6f73cd1a1c8fbd2c7bda2248009b
scorecard: 2026-10-25_0941.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0437 - Lib1515.TestAsync's i < RequestCount can become <= with no test failing, adding a DNS-timeout wait after the last request

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:74`: Lib1515.TestAsync's i < RequestCount can become <= with no test failing, adding a DNS-timeout wait after the last request. Reported by an auditor flagged unreliable in 2026-10-25_0941.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:74`

Sampled mutant (seed 0) survived: 'if (i < RequestCount)' -> 'if (i <= RequestCount)'. Upstream lib1515.c waits (DNS timeout + 1) seconds only between requests, never after the last one. With the mutant the port also waits after the last request, so upstream cases 1515 and 1516 run seconds longer on a real clock. Curl.Conformance.LibTest.UnitTests never checks how many clock waits the port makes.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1515.cs:74:< -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-25: filed proposed.
