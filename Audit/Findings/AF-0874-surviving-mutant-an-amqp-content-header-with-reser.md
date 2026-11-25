---
id: AF-0874
title: Surviving mutant: an AMQP content header with reserved property flag bit 1 set can crash the reader
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0ContentHeader.cs:Read-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0ContentHeader.cs:66:<
task: BL-5538
tasks: BL-5538
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_0602.md
---
# AF-0874 - Surviving mutant: an AMQP content header with reserved property flag bit 1 set can crash the reader

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0ContentHeader.cs:66`: Surviving mutant: an AMQP content header with reserved property flag bit 1 set can crash the reader.

## Evidence

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0ContentHeader.cs:66`

Mutant 'for (int index = 0; index <= PropertiesInFlagOrder.Length; index++)' (from '<') survived in the seeded sample (seed 735398) and again in the targeted -Site run. PropertiesInFlagOrder has 14 entries (bits 15..2, FlagBit(i) = 1 << (15 - i)). Under the mutant, a header whose reserved bit 1 is set indexes PropertiesInFlagOrder[14] and throws IndexOutOfRangeException. The original ignores the bit and goes on to EnsureAtEnd. So a broker frame that curl now reads, or refuses as a syntax error, would end the transfer with an unhandled exception and a different exit code. Tests cover bit 0 ('more property flags') but no header with bit 1 set.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0ContentHeader.cs:66:< -Member Read -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (resolvedLine 66)

## Re-audits

- 2026-11-19 | 2026-11-19_0602.md | reproduces: no | Ran the -Site command: Amqp0ContentHeader.cs:66 (resolvedLine 66), index < PropertiesInFlagOrder.Length -> <=, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-19: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
