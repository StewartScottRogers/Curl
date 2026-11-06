---
id: AF-0693
title: Surviving mutant: curl_url_set can accept a host starting with a delimiter such as '@'
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/UrlPartSetter.cs:CheckedHost-ge:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/UrlPartSetter.cs:152:>=
task: BL-4887
tasks: BL-4887
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0693 - Surviving mutant: curl_url_set can accept a host starting with a delimiter such as '@'

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/UrlPartSetter.cs:152`: Surviving mutant: curl_url_set can accept a host starting with a delimiter such as '@'.

## Evidence

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:152`

': host.AsSpan().IndexOfAny(HostDelimiters) >= 0;' mutated to '> 0' survived. HostDelimiters is "/?#@:". With the mutant, a delimiter at index 0 (e.g. CURLUPART_HOST "@example.com") is no longer refused with CURLUE_BAD_HOSTNAME, and 'http://@example.com/' then parses. No test sets a host whose first character is a delimiter.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartSetter.cs:152:>= -Member CheckedHost -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-05: filed proposed.
