---
id: AF-0488
title: UrlPartSetter.UpperCasedEscapes's index + 2 < text.Length can become <= with no test failing, so a part ending in %X throws
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/UrlPartSetter.cs:UpperCasedEscapes-lt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/UrlPartSetter.cs:262:<
task: none
tasks:
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed: 2026-10-31
closed-how: mechanical
closed-by: 2026-10-31_1149.md
---
# AF-0488 - UrlPartSetter.UpperCasedEscapes's index + 2 < text.Length can become <= with no test failing, so a part ending in %X throws

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/UrlPartSetter.cs:262`: UrlPartSetter.UpperCasedEscapes's index + 2 < text.Length can become <= with no test failing, so a part ending in %X throws.

## Evidence

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:262`

Seed-0 sample of Curl.Engine.UnitLibrary: mutant `index >= 0 && index + 2 < text.Length` -> `index + 2 <= text.Length` survived. UpperCasedEscapes is what Stored() uses for every curl_url_set part set without CURLU_URLENCODE. With the mutant, a value whose last % is second to last (e.g. "a%b") reads text[index + 2] one past the end and throws IndexOutOfRangeException out of curl_url_set instead of storing the text. No test sets a part ending in a percent and one character.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartSetter.cs:262:< -Member UpperCasedEscapes -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | Ran the -Site command: 'killed Curl.Engine.UnitLibrary/UrlPartSetter.cs:262 <' (resolvedLine 262, 'index + 2 < text.Length' mutated to '<='); outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
