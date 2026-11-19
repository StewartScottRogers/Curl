---
id: AF-0880
title: Surviving mutant: clearing a URL handle's scheme can leave it marked as guessed
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/UrlPartSetter.cs:Clearers-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/UrlPartSetter.cs:43:false
task: BL-5544
tasks: BL-5544
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0880 - Surviving mutant: clearing a URL handle's scheme can leave it marked as guessed

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/UrlPartSetter.cs:43`: Surviving mutant: clearing a URL handle's scheme can leave it marked as guessed.

## Evidence

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:43`

Mutant 'parts => parts with { Scheme = null, SchemeGuessed = true }' (from 'false') survived in the Engine sample and in the targeted -Site run. SchemeGuessed decides whether curl_url_get under CURLU_NO_GUESS_SCHEME drops the scheme (ManagedCurlUrlHandle.cs:154, 214) and lets an unsupported scheme through (line 410). So a handle whose scheme was cleared and then read with a default scheme would print a different URL. No test checks SchemeGuessed after clearing CURLUPART_SCHEME.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartSetter.cs:43:false -Member Clearers -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (resolvedLine 43)

## Re-audits

## Log

- 2026-11-19: filed proposed.
