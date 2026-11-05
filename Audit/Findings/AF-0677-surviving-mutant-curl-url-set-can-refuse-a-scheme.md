---
id: AF-0677
title: Surviving mutant: curl_url_set can refuse a scheme of exactly 40 characters as a bad scheme
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/UrlPartSetter.cs:HasSchemeLength-le:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/UrlPartSetter.cs:113:<=
task: none
tasks:
found: 2026-11-05
found-at: aea9f0110c59ad5209aafb9f0f985886a2cb9439
scorecard: 2026-11-05_0819.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0677 - Surviving mutant: curl_url_set can refuse a scheme of exactly 40 characters as a bad scheme

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/UrlPartSetter.cs:113`: Surviving mutant: curl_url_set can refuse a scheme of exactly 40 characters as a bad scheme.

## Evidence

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:113`

Mutant 'value.Length is > 0 and <= MaximumSchemeLength' -> 'value.Length is > 0 and < MaximumSchemeLength' (MaximumSchemeLength = 40, UrlPartSetter.cs:19) survived in the seed-843793 sample, and survived again in the targeted -Site run. SchemeCode returns CurlUrlCode.BadScheme when HasSchemeLength is false, so a 40-character scheme, which curl accepts, would get CURLUE_BAD_SCHEME back from the URL API. The libcurl return code changes and no test pins the boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartSetter.cs:113:<= -Member HasSchemeLength -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant is killed.
- Actual: survived

## Re-audits

## Log

- 2026-11-05: filed proposed.
