---
id: AF-0491
title: OAuth2TokenCacheLocation's summary says it searches curl's own .curlrc variables, but its order and platforms differ from curl's
auditor: truthfulness
severity: Medium
status: accepted
reason: 
key: truthfulness:Curl.Extension.OAuth2.UnitLibrary/Cache/OAuth2TokenCacheLocation.cs:OAuth2TokenCacheLocation:false-doc-comment
reproduction: none
task: BL-4401
tasks: BL-4401
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0491 - OAuth2TokenCacheLocation's summary says it searches curl's own .curlrc variables, but its order and platforms differ from curl's

## Summary

Medium finding from the truthfulness auditor at `Curl.Extension.OAuth2.UnitLibrary/Cache/OAuth2TokenCacheLocation.cs:4`: OAuth2TokenCacheLocation's summary says it searches curl's own .curlrc variables, but its order and platforms differ from curl's. Reported by an auditor flagged unreliable in 2026-10-31_0358.md.

## Evidence

Location: `Curl.Extension.OAuth2.UnitLibrary/Cache/OAuth2TokenCacheLocation.cs:4`

The type's summary (lines 4-5) says 'the first of the environment variables curl searches for its own per-user .curlrc that is set and not empty'. Line 10 searches ["CURL_HOME", "XDG_CONFIG_HOME", "HOME", "APPDATA", "USERPROFILE"] on every OS. Curl's own port of curl's conf_list, Curl.Cli.UnitLibrary/DefaultConfigFileSearch.cs:52-57, searches CURL_HOME, XDG_CONFIG_HOME, HOME, then USERPROFILE before APPDATA, and both of those only on Windows (WindowsOnly = true). With both USERPROFILE and APPDATA set, or off Windows with only APPDATA or USERPROFILE set, the cache lands in a directory curl's search would not pick, while the doc tells a reader it matches curl. The test OAuth2TokenCacheLocationTests pins the divergent order. Phase 2: ADR-1034 line 18 makes the same claim with the same order, so the ADR is also false against DefaultConfigFileSearch.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OAuth2.UnitLibrary/Cache/OAuth2TokenCacheLocation.cs, Curl.Cli.UnitLibrary/DefaultConfigFileSearch.cs -Pattern 'SearchedVariables =', '\("(USERPROFILE|APPDATA)", string.Empty'
```

- Expected: The two lists agree: OAuth2TokenCacheLocation searches USERPROFILE before APPDATA, Windows-only as curl does, or its doc no longer says it follows curl's .curlrc search.
- Actual: OAuth2TokenCacheLocation.cs:10 lists ... "HOME", "APPDATA", "USERPROFILE"; DefaultConfigFileSearch.cs:55 ("USERPROFILE", string.Empty, false, true) comes before :56 ("APPDATA", string.Empty, false, true).

## Re-audits

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
