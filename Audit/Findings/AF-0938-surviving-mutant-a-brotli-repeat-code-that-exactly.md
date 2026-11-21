---
id: AF-0938
title: Surviving mutant: a Brotli repeat code that exactly fills its alphabet can be refused
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliSymbolLengthReader.cs:AddRepeat-gt:surviving-mutant
reproduction: mutation Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliSymbolLengthReader.cs:55:>
task: BL-5697
tasks: BL-5697
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0938 - Surviving mutant: a Brotli repeat code that exactly fills its alphabet can be refused

## Summary

High finding from the quality auditor at `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliSymbolLengthReader.cs:55`: Surviving mutant: a Brotli repeat code that exactly fills its alphabet can be refused. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliSymbolLengthReader.cs:55`

Drawn library, seed 812510: if (added > alphabetSize - _symbol) mutated to >= survived. A repeat ending exactly at the alphabet's end is valid (RFC 7932 section 3.5); the mutant throws BrotliFormatException, so a valid dcb response fails with bad content encoding instead of writing its body.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliSymbolLengthReader.cs:55:> -Member AddRepeat -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
