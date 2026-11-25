---
id: AF-0994
title: Surviving mutant: curl_url_set can refuse a value of exactly 8,000,000 characters with CURLUE_MALFORMED_INPUT (> -> >=)
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:Set-gt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:97:>
task: BL-5988
tasks: BL-5988
found: 2026-11-25
found-at: eb33fe3c5ab162945003079cf4f807e5608dde01
scorecard: 2026-11-25_1306.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0994 - Surviving mutant: curl_url_set can refuse a value of exactly 8,000,000 characters with CURLUE_MALFORMED_INPUT (> -> >=)

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:97`: Surviving mutant: curl_url_set can refuse a value of exactly 8,000,000 characters with CURLUE_MALFORMED_INPUT (> -> >=).

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:97`

Mutant 'if (value?.Length > MaximumInputLength)' -> 'if (value?.Length >= MaximumInputLength)' (MaximumInputLength = 8_000_000, curl's CURL_MAX_INPUT_LENGTH) survived every Curl.Engine.UnitTests test. A part value of exactly the limit then returns CurlUrlCode.MalformedInput instead of being accepted, which changes the code curl_url_set returns to a libcurl caller. No test pins the boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:97:> -Member Set -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-25: filed proposed.
