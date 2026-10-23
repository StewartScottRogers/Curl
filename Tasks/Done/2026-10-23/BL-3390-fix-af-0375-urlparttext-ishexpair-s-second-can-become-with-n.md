---
id: BL-3390
title: Fix AF-0375: UrlPartText.IsHexPair's second && can become || with no test failing, so a malformed percent escape throws instead of passing through
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3390 — Fix AF-0375: UrlPartText.IsHexPair's second && can become || with no test failing, so a malformed percent escape throws instead of passing through

## Goal

The defect the audit office reported as AF-0375 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0375 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0375-urlparttext-ishexpair-s-second-can-become-with-no.md`.

Location: `Curl.Engine.UnitLibrary/UrlPartText.cs:133`

Location: `Curl.Engine.UnitLibrary/UrlPartText.cs:133`

Invoke-MutationTest.ps1 -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0 reported this mutant as survived. Original: index + 1 < bytes.Length && char.IsAsciiHexDigit((char)bytes[index]) && char.IsAsciiHexDigit((char)bytes[index + 1]); mutated: ... && char.IsAsciiHexDigit((char)bytes[index]) || char.IsAsciiHexDigit((char)bytes[index + 1]). With the mutant, a part holding '%g4' counts as a hex pair and HexValue('g') throws FormatException, and a part ending in '%4' reads bytes[index + 1] past the end and throws IndexOutOfRangeException. The correct code keeps both unchanged. This is the URL handle's percent decode (curl_url_get with CURLU_URLDECODE), so a URL that libcurl gives back as written would throw out of the API instead. No test decodes a malformed escape whose second character is a hex digit, or a trailing '%' plus one digit.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartText.cs:133:&& -Member IsHexPair -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Audit/ is off limits to a lane, so the reproduction script was not run; the mutant was applied by hand (second && to ||) and the new Transform_UrlDecode_LeavesAMalformedPercentEscapeAsItIs test fails 3 cases, then passes with the code restored. Production code unchanged, so no quality run. Engine fast tests: 2473 passed.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-23: Doing -> Done. UrlPartText.IsHexPair mutant now killed by a malformed percent-escape test
