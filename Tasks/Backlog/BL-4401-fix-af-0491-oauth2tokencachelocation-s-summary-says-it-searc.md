---
id: BL-4401
title: Fix AF-0491: OAuth2TokenCacheLocation's summary says it searches curl's own .curlrc variables, but its order and platforms differ from curl's
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Extension.OAuth2.UnitLibrary]
requirement: none
created: 2026-10-31
completed:
---
# BL-4401 — Fix AF-0491: OAuth2TokenCacheLocation's summary says it searches curl's own .curlrc variables, but its order and platforms differ from curl's

## Goal

The defect the audit office reported as AF-0491 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0491 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0491-oauth2tokencachelocation-s-summary-says-it-searche.md`.

Location: `Curl.Extension.OAuth2.UnitLibrary/Cache/OAuth2TokenCacheLocation.cs:4`

Location: `Curl.Extension.OAuth2.UnitLibrary/Cache/OAuth2TokenCacheLocation.cs:4`

The type's summary (lines 4-5) says 'the first of the environment variables curl searches for its own per-user .curlrc that is set and not empty'. Line 10 searches ["CURL_HOME", "XDG_CONFIG_HOME", "HOME", "APPDATA", "USERPROFILE"] on every OS. Curl's own port of curl's conf_list, Curl.Cli.UnitLibrary/DefaultConfigFileSearch.cs:52-57, searches CURL_HOME, XDG_CONFIG_HOME, HOME, then USERPROFILE before APPDATA, and both of those only on Windows (WindowsOnly = true). With both USERPROFILE and APPDATA set, or off Windows with only APPDATA or USERPROFILE set, the cache lands in a directory curl's search would not pick, while the doc tells a reader it matches curl. The test OAuth2TokenCacheLocationTests pins the divergent order. Phase 2: ADR-1034 line 18 makes the same claim with the same order, so the ADR is also false against DefaultConfigFileSearch.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OAuth2.UnitLibrary/Cache/OAuth2TokenCacheLocation.cs, Curl.Cli.UnitLibrary/DefaultConfigFileSearch.cs -Pattern 'SearchedVariables =', '\("(USERPROFILE|APPDATA)", string.Empty'
```

- Expected: The two lists agree: OAuth2TokenCacheLocation searches USERPROFILE before APPDATA, Windows-only as curl does, or its doc no longer says it follows curl's .curlrc search.
- Actual: OAuth2TokenCacheLocation.cs:10 lists ... "HOME", "APPDATA", "USERPROFILE"; DefaultConfigFileSearch.cs:55 ("USERPROFILE", string.Empty, false, true) comes before :56 ("APPDATA", string.Empty, false, true).

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
