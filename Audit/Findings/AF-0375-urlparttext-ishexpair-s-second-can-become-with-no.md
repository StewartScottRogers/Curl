---
id: AF-0375
title: UrlPartText.IsHexPair's second && can become || with no test failing, so a malformed percent escape throws instead of passing through
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-23_2322.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/UrlPartText.cs:IsHexPair-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/UrlPartText.cs:133:&&
task: none
tasks:
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed: 2026-10-23
closed-how: mechanical
closed-by: 2026-10-23_2322.md
---
# AF-0375 - UrlPartText.IsHexPair's second && can become || with no test failing, so a malformed percent escape throws instead of passing through

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/UrlPartText.cs:133`: UrlPartText.IsHexPair's second && can become || with no test failing, so a malformed percent escape throws instead of passing through.

## Evidence

Location: `Curl.Engine.UnitLibrary/UrlPartText.cs:133`

Invoke-MutationTest.ps1 -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0 reported this mutant as survived. Original: index + 1 < bytes.Length && char.IsAsciiHexDigit((char)bytes[index]) && char.IsAsciiHexDigit((char)bytes[index + 1]); mutated: ... && char.IsAsciiHexDigit((char)bytes[index]) || char.IsAsciiHexDigit((char)bytes[index + 1]). With the mutant, a part holding '%g4' counts as a hex pair and HexValue('g') throws FormatException, and a part ending in '%4' reads bytes[index + 1] past the end and throws IndexOutOfRangeException. The correct code keeps both unchanged. This is the URL handle's percent decode (curl_url_get with CURLU_URLDECODE), so a URL that libcurl gives back as written would throw out of the API instead. No test decodes a malformed escape whose second character is a hex digit, or a trailing '%' plus one digit.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartText.cs:133:&& -Member IsHexPair -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-23 | 2026-10-23_2322.md | reproduces: no | Ran the -Site reproduction: UrlPartText.cs:133 'index + 1 < bytes.Length && ...' -> '... || ...' was killed (resolvedLine 133). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> closed. Re-audit 2026-10-23_2322.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
