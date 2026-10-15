---
id: BL-2254
title: Fix AF-0217: HttpRedirectLocation.AppendEscaped's Latin-1 bound '<= 0xFF' can become '< 0xFF' with no test failing, so a redirect would send U+00FF as %C3%BF
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2254 — Fix AF-0217: HttpRedirectLocation.AppendEscaped's Latin-1 bound '<= 0xFF' can become '< 0xFF' with no test failing, so a redirect would send U+00FF as %C3%BF

## Goal

The defect the audit office reported as AF-0217 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0217 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0217-httpredirectlocation-appendescaped-s-latin-1-bound.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:260`

Location: `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:260`

Sampled run (seed 0, 40 mutants) of Curl.Protocol.Http.UnitLibrary: mutant 'if (value[index] <= 'ÿ')' -> 'if (value[index] < 'ÿ')' in AppendEscaped survived. A Location header is read as Latin-1, one character per byte, and each byte above 0x7E must be escaped as itself (%FF). With the mutant, the byte 0xFF falls through to the UTF-8 branch and is sent as %C3%BF. That changes the request target of the followed redirect (request bytes) with every test green. No test sends a 0xFF byte in a Location.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs:260:<= -Member AppendEscaped -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
