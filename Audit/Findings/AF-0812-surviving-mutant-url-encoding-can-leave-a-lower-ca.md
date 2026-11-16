---
id: AF-0812
title: Surviving mutant: URL encoding can leave a lower-case %xx escape un-upper-cased, or index before the string
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/UrlPartText.cs:IsLowerCaseEscape-plus1:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/UrlPartText.cs:218:+1
task: none
tasks:
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0812 - Surviving mutant: URL encoding can leave a lower-case %xx escape un-upper-cased, or index before the string

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/UrlPartText.cs:218`: Surviving mutant: URL encoding can leave a lower-case %xx escape un-upper-cased, or index before the string.

## Evidence

Location: `Curl.Engine.UnitLibrary/UrlPartText.cs:218`

In IsLowerCaseEscape, `bytes[index + 1]` changed to `bytes[index - 1]` survived. AppendEncodedByte uses it to upper-case an escape as urlencode_str does. With the mutant, '%aF' keeps its lower-case digit unless the byte before '%' happens to be lower-case, and an escape at index 0 reads bytes[-1] and throws. The URL text the engine produces and sends changes, and no test pins an escape whose first hex digit alone is lower-case.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartText.cs:218:+1 -Member IsLowerCaseEscape -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
