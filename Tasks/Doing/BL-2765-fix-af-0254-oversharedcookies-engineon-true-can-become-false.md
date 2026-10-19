---
id: BL-2765
title: Fix AF-0254: OverSharedCookies' engineOn: true can become false with no test failing, turning the cookie engine off for share-handle cookies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/CookieEngine.cs, Curl.Engine.UnitTests/CookieEngineTests.cs]
requirement: none
created: 2026-10-18
completed:
---
# BL-2765 — Fix AF-0254: OverSharedCookies' engineOn: true can become false with no test failing, turning the cookie engine off for share-handle cookies

## Goal

The defect the audit office reported as AF-0254 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0254 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0254-oversharedcookies-engineon-true-can-become-false-w.md`.

Location: `Curl.Engine.UnitLibrary/CookieEngine.cs:133`

Location: `Curl.Engine.UnitLibrary/CookieEngine.cs:133`

Engine sample, seed 0: survived. Original `new(http, sharedCookies, engineOn: true);`, mutated `engineOn: false`. The doc comment says the share's cookies apply CURLOPT_COOKIELIST items 'with the cookie engine on whatever the handle's options say' (upstream test506). With the mutant they are applied with the engine off, which can drop cookies a shared handle should send. No test pins it.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/CookieEngine.cs:133:true -Member OverSharedCookies -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary to files, by reading CookieEngine.OverSharedCookies (line 133), so it can run beside other Engine tasks.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
