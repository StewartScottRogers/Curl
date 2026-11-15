---
id: AF-0754
title: Surviving mutant: curl_url_set can accept a URL whose first character is a control byte
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:HoldsControlByte-ge:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:322:>=
task: BL-5203
tasks: BL-5203
found: 2026-11-14
found-at: fc4d1268bd4c1bce123dba90334b07e141be698a
scorecard: 2026-11-14_1335.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0754 - Surviving mutant: curl_url_set can accept a URL whose first character is a control byte

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:322`: Surviving mutant: curl_url_set can accept a URL whose first character is a control byte.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:322`

Mutant 'value.AsSpan().IndexOfAnyInRange('\u0000', '\u001F') >= 0' -> '> 0' survived. A control byte at index 0 would no longer be MalformedInput (curl's junkscan) and the URL would parse, so a CURLUcode a libcurl caller sees changes. The tests reject control bytes only after the first character.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:322:>= -Member HoldsControlByte -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-14: filed proposed.
