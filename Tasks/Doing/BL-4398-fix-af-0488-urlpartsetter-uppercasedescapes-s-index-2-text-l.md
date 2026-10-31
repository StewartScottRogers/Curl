---
id: BL-4398
title: Fix AF-0488: UrlPartSetter.UpperCasedEscapes's index + 2 < text.Length can become <= with no test failing, so a part ending in %X throws
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-31
completed:
---
# BL-4398 — Fix AF-0488: UrlPartSetter.UpperCasedEscapes's index + 2 < text.Length can become <= with no test failing, so a part ending in %X throws

## Goal

The defect the audit office reported as AF-0488 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0488 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0488-urlpartsetter-uppercasedescapes-s-index-2-text-len.md`.

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:262`

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:262`

Seed-0 sample of Curl.Engine.UnitLibrary: mutant `index >= 0 && index + 2 < text.Length` -> `index + 2 <= text.Length` survived. UpperCasedEscapes is what Stored() uses for every curl_url_set part set without CURLU_URLENCODE. With the mutant, a value whose last % is second to last (e.g. "a%b") reads text[index + 2] one past the end and throws IndexOutOfRangeException out of curl_url_set instead of storing the text. No test sets a part ending in a percent and one character.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartSetter.cs:262:< -Member UpperCasedEscapes -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
