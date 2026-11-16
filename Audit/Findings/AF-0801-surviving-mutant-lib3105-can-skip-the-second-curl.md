---
id: AF-0801
title: Surviving mutant: lib3105 can skip the second curl_multi_remove_handle it exists to check
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib3105.cs:RemoveTwice-eq:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib3105.cs:65:==
task: none
tasks:
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0801 - Surviving mutant: lib3105 can skip the second curl_multi_remove_handle it exists to check

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib3105.cs:65`: Surviving mutant: lib3105 can skip the second curl_multi_remove_handle it exists to check.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib3105.cs:65`

The seed 770877 sample of Curl.Conformance.LibTest.UnitLibrary: 'if (code == CurlMultiCode.Ok)' -> 'if (code != CurlMultiCode.Ok)' in RemoveTwice survived. With the mutant, a first removal that succeeds is never followed by the second, so the port no longer checks that removing a handle twice answers OK. A Libcurl regression there would pass the upstream case silently. No test checks that RemoveHandle is called twice or makes the second call fail.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib3105.cs:65:== -Member RemoveTwice -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
