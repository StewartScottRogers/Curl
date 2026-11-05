---
id: BL-4868
title: Fix AF-0677: Surviving mutant: curl_url_set can refuse a scheme of exactly 40 characters as a bad scheme
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-05
completed:
---
# BL-4868 — Fix AF-0677: Surviving mutant: curl_url_set can refuse a scheme of exactly 40 characters as a bad scheme

## Goal

The defect the audit office reported as AF-0677 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0677 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0677-surviving-mutant-curl-url-set-can-refuse-a-scheme.md`.

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:113`

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:113`

Mutant 'value.Length is > 0 and <= MaximumSchemeLength' -> 'value.Length is > 0 and < MaximumSchemeLength' (MaximumSchemeLength = 40, UrlPartSetter.cs:19) survived in the seed-843793 sample, and survived again in the targeted -Site run. SchemeCode returns CurlUrlCode.BadScheme when HasSchemeLength is false, so a 40-character scheme, which curl accepts, would get CURLUE_BAD_SCHEME back from the URL API. The libcurl return code changes and no test pins the boundary.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartSetter.cs:113:<= -Member HasSchemeLength -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant is killed.
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-05: Created.
