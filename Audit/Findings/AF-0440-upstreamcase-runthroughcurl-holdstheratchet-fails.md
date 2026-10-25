---
id: AF-0440
title: UpstreamCase_RunThroughCurl_HoldsTheRatchet fails unmutated in the fast tests
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.UnitTests/UpstreamConformanceTests.cs:UpstreamCase_RunThroughCurl_HoldsTheRatchet:baseline-failure
reproduction: none
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
# AF-0440 - UpstreamCase_RunThroughCurl_HoldsTheRatchet fails unmutated in the fast tests

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs:96`: UpstreamCase_RunThroughCurl_HoldsTheRatchet fails unmutated in the fast tests. Reported by an auditor flagged unreliable in 2026-10-25_0941.md.

## Evidence

Location: `Curl.Conformance.UnitTests/UpstreamConformanceTests.cs:96`

Invoke-MutationTest.ps1 -Library Curl.Conformance.UnitLibrary -ExcludeBaselineFailures printed 'left out 1 tests that fail unmutated: Curl.Conformance.UpstreamConformanceTests.UpstreamCase_RunThroughCurl_HoldsTheRatchet'. The targeted Conformance re-audit runs (AF-0419, AF-0420, AF-0428, AF-0429) listed the same test in excludedTests. This is a fast test, not an Integration one, so CI's fast run should fail on this commit. Every Conformance mutant was judged without it.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration&Name~UpstreamCase_RunThroughCurl_HoldsTheRatchet"
```

- Expected: Every row passes or is inconclusive.
- Actual: At least one row fails (named as a baseline failure by Invoke-MutationTest.ps1).

## Re-audits

## Log

- 2026-10-25: filed proposed.
