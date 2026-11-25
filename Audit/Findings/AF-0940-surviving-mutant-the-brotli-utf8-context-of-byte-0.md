---
id: AF-0940
title: Surviving mutant: the Brotli UTF8 context of byte 0xC0 can be wrong, mis-decoding output
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliLiteralContext.cs:BuildUtf8LastByte-ge:surviving-mutant
reproduction: mutation Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliLiteralContext.cs:35:>=
task: BL-5699
tasks: BL-5699
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed: 2026-11-21
closed-how: mechanical
closed-by: 2026-11-21_1740.md
---
# AF-0940 - Surviving mutant: the Brotli UTF8 context of byte 0xC0 can be wrong, mis-decoding output

## Summary

High finding from the quality auditor at `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliLiteralContext.cs:35`: Surviving mutant: the Brotli UTF8 context of byte 0xC0 can be wrong, mis-decoding output. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliLiteralContext.cs:35`

Drawn library, seed 812510: (value >= 192 ? 2 : 0) mutated to value > 192 survived. Byte 0xC0 then gets context 0 instead of 2 (RFC 7932 section 7.1), so a UTF8-mode stream whose previous byte is 0xC0 picks the wrong literal tree and decodes wrong bytes. No test covers the 192 boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CompressionDictionary.UnitLibrary/Brotli/BrotliLiteralContext.cs:35:>= -Member BuildUtf8LastByte -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-21 | 2026-11-21_1740.md | reproduces: no | Ran the -Site command: resolvedLine 35, mutant '(value > 192 ? 2 : 0)' killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-21: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
