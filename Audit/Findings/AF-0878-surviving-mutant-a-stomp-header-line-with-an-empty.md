---
id: AF-0878
title: Surviving mutant: a STOMP header line with an empty name (':value') can be refused as having no colon
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameEncoding.cs:ParseHeader-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameEncoding.cs:75:<
task: BL-5542
tasks: BL-5542
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_0602.md
---
# AF-0878 - Surviving mutant: a STOMP header line with an empty name (':value') can be refused as having no colon

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameEncoding.cs:75`: Surviving mutant: a STOMP header line with an empty name (':value') can be refused as having no colon.

## Evidence

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameEncoding.cs:75`

Mutant 'if (colon <= 0)' (from '<') survived in the sample and in the targeted -Site run. A header line starting with ':' is read today as StompHeader("", value). Under the mutant it throws FormatException 'STOMP header line without a colon', failing the transfer. ParseHeader_NoColon_IsAFormatError covers only 'nocolon'; no test parses a line whose colon is at index 0.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Stomp.UnitLibrary/Wire/StompFrameEncoding.cs:75:< -Member ParseHeader -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (resolvedLine 75)

## Re-audits

- 2026-11-19 | 2026-11-19_0602.md | reproduces: no | Ran the -Site command: StompFrameEncoding.cs:75 (resolvedLine 75), if (colon < 0) -> if (colon <= 0), outcome killed (ParseHeader_ColonAtIndexZero_GivesAnEmptyNameAndTheValue). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-19: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
