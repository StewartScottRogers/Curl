---
id: AF-0943
title: Surviving mutant: a malformed inner list in Use-As-Dictionary can be accepted
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.CompressionDictionary.UnitLibrary/StructuredFieldDictionaryParser.cs:ParseInnerList-or:surviving-mutant
reproduction: mutation Curl.Extension.CompressionDictionary.UnitLibrary/StructuredFieldDictionaryParser.cs:138:||
task: BL-5702
tasks: BL-5702
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0943 - Surviving mutant: a malformed inner list in Use-As-Dictionary can be accepted

## Summary

Medium finding from the quality auditor at `Curl.Extension.CompressionDictionary.UnitLibrary/StructuredFieldDictionaryParser.cs:138`: Surviving mutant: a malformed inner list in Use-As-Dictionary can be accepted. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/StructuredFieldDictionaryParser.cs:138`

Drawn library, seed 812510: if (item is null || !IsInnerListItemEnd()) mutated to && survived; an inner-list item followed by a non-separator is no longer refused, so an invalid header (RFC 8941 section 4.2.1.2) can be stored as a dictionary. No test parses such a list.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CompressionDictionary.UnitLibrary/StructuredFieldDictionaryParser.cs:138:|| -Member ParseInnerList -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
