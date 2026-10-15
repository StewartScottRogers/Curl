---
id: AF-0217
title: HttpRedirectLocation.AppendEscaped's Latin-1 bound '<= 0xFF' can become '< 0xFF' with no test failing, so a redirect would send U+00FF as %C3%BF
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-15_0156.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:AppendEscaped-le:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:260:<=
task: BL-2254
tasks: BL-2254
found: 2026-10-14
found-at: d86a80f0091fbf88c08d7b9b2fa37a9197854bd8
scorecard: 2026-10-14_1815.md
duplicate-of:
closed: 2026-10-15
closed-how: mechanical
closed-by: 2026-10-15_0156.md
---
# AF-0217 - HttpRedirectLocation.AppendEscaped's Latin-1 bound '<= 0xFF' can become '< 0xFF' with no test failing, so a redirect would send U+00FF as %C3%BF

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:260`: HttpRedirectLocation.AppendEscaped's Latin-1 bound '<= 0xFF' can become '< 0xFF' with no test failing, so a redirect would send U+00FF as %C3%BF. Reported by an auditor flagged unreliable in 2026-10-14_1815.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:260`

Sampled run (seed 0, 40 mutants) of Curl.Protocol.Http.UnitLibrary: mutant 'if (value[index] <= 'ÿ')' -> 'if (value[index] < 'ÿ')' in AppendEscaped survived. A Location header is read as Latin-1, one character per byte, and each byte above 0x7E must be escaped as itself (%FF). With the mutant, the byte 0xFF falls through to the UTF-8 branch and is sent as %C3%BF. That changes the request target of the followed redirect (request bytes) with every test green. No test sends a 0xFF byte in a Location.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:260:<= -Member AppendEscaped -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-15 | 2026-10-15_0156.md | reproduces: no | Ran the targeted -Site command (HttpRedirectLocation.cs:260:<=, -Member AppendEscaped): resolvedLine 260, 'if (value[index] <= 'ÿ')' -> 'if (value[index] < 'ÿ')', outcome killed (score 1). Baseline passed with no excluded tests. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-14: filed proposed.
- 2026-10-14: proposed -> accepted.
- 2026-10-15: accepted -> closed. Re-audit 2026-10-15_0156.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
