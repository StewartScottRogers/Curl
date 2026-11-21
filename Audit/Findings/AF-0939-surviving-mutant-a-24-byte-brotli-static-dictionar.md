---
id: AF-0939
title: Surviving mutant: a 24-byte Brotli static-dictionary word can be refused
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliStaticDictionary.cs:WriteWord-gt:surviving-mutant
reproduction: mutation Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliStaticDictionary.cs:57:>
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
# AF-0939 - Surviving mutant: a 24-byte Brotli static-dictionary word can be refused

## Summary

High finding from the quality auditor at `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliStaticDictionary.cs:57`: Surviving mutant: a 24-byte Brotli static-dictionary word can be refused. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliStaticDictionary.cs:57`

Drawn library, seed 812510: length > MaximumWordLength (24) mutated to >= survived. RFC 7932 section 8 allows copy lengths 4 to 24; the mutant refuses every 24-byte word and fails valid streams. No test decodes a 24-byte static word.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliStaticDictionary.cs:57:> -Member WriteWord -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
