---
id: AF-0254
title: OverSharedCookies' engineOn: true can become false with no test failing, turning the cookie engine off for share-handle cookies
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/CookieEngine.cs:OverSharedCookies-true:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/CookieEngine.cs:133:true
task: none
tasks:
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0254 - OverSharedCookies' engineOn: true can become false with no test failing, turning the cookie engine off for share-handle cookies

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/CookieEngine.cs:133`: OverSharedCookies' engineOn: true can become false with no test failing, turning the cookie engine off for share-handle cookies. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/CookieEngine.cs:133`

Engine sample, seed 0: survived. Original `new(http, sharedCookies, engineOn: true);`, mutated `engineOn: false`. The doc comment says the share's cookies apply CURLOPT_COOKIELIST items 'with the cookie engine on whatever the handle's options say' (upstream test506). With the mutant they are applied with the engine off, which can drop cookies a shared handle should send. No test pins it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/CookieEngine.cs:133:true -Member OverSharedCookies -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
