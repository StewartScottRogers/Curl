---
id: BL-2254
title: Fix AF-0217: HttpRedirectLocation.AppendEscaped's Latin-1 bound '<= 0xFF' can become '< 0xFF' with no test failing, so a redirect would send U+00FF as %C3%BF
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-14
completed: 2026-10-14
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The fix is a test, not a production change: `HttpRedirectLocationTests.Resolve_OtherLocation_FollowsRfc3986` gains two boundary rows, `/aÿ` -> `%FF` (the last Latin-1 byte, escaped as itself) and `/aĀ` -> `%C4%80` (the first character above Latin-1, as UTF-8). `HttpRedirectLocation.cs` is unchanged, so no quality run was needed.
- Added `Curl.Protocol.Http.UnitTests` to `touches`: the test lives there. No other task in Doing on origin/work/dark-factory names it (only BL-2241, `Curl.Conformance.UnitTests`).
- Lanes may not run `Audit/Tools/Invoke-MutationTest.ps1` (the audit guard refuses it), so the mutant was applied by hand instead: with line 260 changed to `< 'ÿ'`, the new row "byte 0xFF, the last Latin-1 byte, escaped as itself" fails (1 failed, 54 passed); with the line restored, all pass. The quality auditor's re-audit closes the finding.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. A redirect Location holding byte 0xFF is now pinned to %FF by a test, so the AF-0217 mutant '< 0xFF' fails a test
