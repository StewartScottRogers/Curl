---
id: BL-2935
title: Fix AF-0297: UpperCasedEscapes' && can become || with no test failing, upper-casing escapes that are not two hex digits
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-21
completed:
---
# BL-2935 — Fix AF-0297: UpperCasedEscapes' && can become || with no test failing, upper-casing escapes that are not two hex digits

## Goal

The defect the audit office reported as AF-0297 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0297 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0297-uppercasedescapes-can-become-with-no-test-failing.md`.

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:234`

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:234`

Sampled mutant (seed 0) survived: if (char.IsAsciiHexDigit(text[index + 1]) && char.IsAsciiHexDigit(text[index + 2])) => ||. A URL part holding a malformed escape such as %ag would be rewritten to %AG, which changes the URL the handle gives back and sends. No test sets a part with a half-hex escape.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartSetter.cs:234:&& -Member UpperCasedEscapes -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
