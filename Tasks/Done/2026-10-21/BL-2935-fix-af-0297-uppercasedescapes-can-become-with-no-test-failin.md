---
id: BL-2935
title: Fix AF-0297: UpperCasedEscapes' && can become || with no test failing, upper-casing escapes that are not two hex digits
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlPartSetter.cs, Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The fix is a test only: `Set_TextPartWithAnEscapeOfOnlyOneHexDigit_LeavesItsCaseAsItIs` sets a fragment of `%ag` and `%ga` and expects it back unchanged, which kills the `&&` to `||` mutant (`%ag` would become `%AG`). Production code was already right.
- Touches narrowed from the whole Curl.Engine.UnitLibrary to UrlPartSetter.cs, and widened to Curl.Engine.UnitTests/ManagedCurlUrlHandleTests.Set.cs, where the test belongs; no task in Doing on origin/work/dark-factory names either.
- The audit guard refuses a lane running `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand (line 234 `&&` to `||`): both new DataRows failed, 238 others passed; with the line restored all 240 pass. The quality auditor's re-audit runs the tool itself.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Done. A URL part set with a one-hex-digit escape (%ag, %ga) is now pinned unchanged, killing AF-0297's && to || mutant
