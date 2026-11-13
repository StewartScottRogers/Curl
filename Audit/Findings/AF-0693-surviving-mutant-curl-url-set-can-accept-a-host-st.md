---
id: AF-0693
title: Surviving mutant: curl_url_set can accept a host starting with a delimiter such as '@'
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/UrlPartSetter.cs:CheckedHost-ge:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/UrlPartSetter.cs:152:>=
task: BL-4887
tasks: BL-4887
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed: 2026-11-12
closed-how: mechanical
closed-by: 2026-11-12_2105.md
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

- 2026-11-12 | 2026-11-12_2105.md | reproduces: no | Ran the -Site command: UrlPartSetter.cs:152 'IndexOfAny(HostDelimiters) >= 0' -> '> 0' was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-05: filed proposed.
- 2026-11-12: accepted -> closed. Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
