---
id: AF-0491
title: OAuth2TokenCacheLocation's summary says it searches curl's own .curlrc variables, but its order and platforms differ from curl's
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Extension.OAuth2.UnitLibrary/Cache/OAuth2TokenCacheLocation.cs:OAuth2TokenCacheLocation:false-doc-comment
reproduction: none
task: BL-4401
tasks: BL-4401
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed: 2026-11-01
closed-how: reliable-reaudit
closed-by: 2026-11-01_0144.md
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

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | Ran the reproduction. OAuth2TokenCacheLocation.SearchedVariables is CURL_HOME, XDG_CONFIG_HOME, HOME, USERPROFILE, APPDATA, with the last two Windows-only (WindowsOnlyFrom = 3). DefaultConfigFileSearch.cs:52-56 has the same order, with USERPROFILE and APPDATA marked WindowsOnly true. The summary's claim to follow curl's .curlrc order and platforms is now true.
- 2026-11-01 | 2026-11-01_0144.md | reproduces: no | Ran the reproduction. SearchedVariables is [CURL_HOME, XDG_CONFIG_HOME, HOME, USERPROFILE, APPDATA] (OAuth2TokenCacheLocation.cs:13), and DefaultConfigFileSearch.cs:52-56 searches CURL_HOME, XDG_CONFIG_HOME, HOME, then USERPROFILE and APPDATA as WindowsOnly=true. The summary now says 'on Windows only as in curl', and FindDefaultDirectory searches the last two only on Windows (WindowsOnlyFrom = 3). Order and platforms now match curl's first five directories.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
- 2026-11-01: accepted -> closed. Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces.
