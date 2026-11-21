---
id: AF-0942
title: Surviving mutant: a ':' with no name after it can be compiled as a URL-pattern group
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryMatchPattern.cs:AppendPatternPart-gt:surviving-mutant
reproduction: mutation Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryMatchPattern.cs:82:>
task: none
tasks:
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0942 - Surviving mutant: a ':' with no name after it can be compiled as a URL-pattern group

## Summary

Medium finding from the quality auditor at `Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryMatchPattern.cs:82`: Surviving mutant: a ':' with no name after it can be compiled as a URL-pattern group. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryMatchPattern.cs:82`

Drawn library, seed 812510: character == ':' && nameEnd > index + 1 mutated to >= survived. A bare ':' then becomes a wildcard segment instead of a literal colon, so a dictionary matches URLs it should not and its Available-Dictionary header is sent on them. No test has a match pattern with a bare colon.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CompressionDictionary.UnitLibrary/DictionaryMatchPattern.cs:82:> -Member AppendPatternPart -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
