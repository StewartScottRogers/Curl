---
id: AF-0650
title: Host-mapping test names exit 3 but asserts only a false return
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:TryNormalizeReadingHost_MeasuredArabicFormCurlRefusesWithExit3_IsRefused:name-lies
reproduction: none
task: BL-4832
tasks: BL-4832
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0650 - Host-mapping test names exit 3 but asserts only a false return

## Summary

High finding from the quality auditor at `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:568`: Host-mapping test names exit 3 but asserts only a false return.

## Evidence

Location: `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:568`

The name states curl refuses with exit 3. The only assertion is Assert.IsFalse(accepted). Exit 3 is never checked in this test or a helper it calls.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs -Pattern 'RefusesWithExit3_IsRefused' -Context 0,5
```

- Expected: An assertion of exit 3 (CurlExitCode.UrlMalformat), or a name without the number.
- Actual: Assert.IsFalse(accepted);

## Re-audits

- 2026-11-04 | 2026-11-04_1442.md | reproduces: no | Ran the Select-String: no match. No test in CurlUrlHostNameMappingTests.cs names Exit3 any more; the refusal test is now TryNormalizeReadingHost_MeasuredArabicFormCurlRefuses_IsRefusedAsIdnConversionFailed (line 568), asserting Assert.IsFalse(accepted) and Assert.AreEqual(CurlUrlRejection.IdnConversionFailed, rejection).

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
