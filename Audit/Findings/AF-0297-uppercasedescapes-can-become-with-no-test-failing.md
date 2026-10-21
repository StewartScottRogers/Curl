---
id: AF-0297
title: UpperCasedEscapes' && can become || with no test failing, upper-casing escapes that are not two hex digits
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/UrlPartSetter.cs:UpperCasedEscapes-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/UrlPartSetter.cs:234:&&
task: BL-2935
tasks: BL-2935
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0297 - UpperCasedEscapes' && can become || with no test failing, upper-casing escapes that are not two hex digits

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/UrlPartSetter.cs:234`: UpperCasedEscapes' && can become || with no test failing, upper-casing escapes that are not two hex digits.

## Evidence

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:234`

Sampled mutant (seed 0) survived: if (char.IsAsciiHexDigit(text[index + 1]) && char.IsAsciiHexDigit(text[index + 2])) => ||. A URL part holding a malformed escape such as %ag would be rewritten to %AG, which changes the URL the handle gives back and sends. No test sets a part with a half-hex escape.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/UrlPartSetter.cs:234:&& -Member UpperCasedEscapes -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
