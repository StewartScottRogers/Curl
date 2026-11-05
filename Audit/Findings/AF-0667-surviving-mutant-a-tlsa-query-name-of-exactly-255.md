---
id: AF-0667
title: Surviving mutant: a TLSA query name of exactly 255 octets can be refused as too long
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-05_0819.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.Dane.UnitLibrary/Wire/DnssecQueryEncoder.cs:EncodeName-gt:surviving-mutant
reproduction: mutation Curl.Extension.Dane.UnitLibrary/Wire/DnssecQueryEncoder.cs:80:>
task: BL-4855
tasks: BL-4855
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed: 2026-11-05
closed-how: mechanical
closed-by: 2026-11-05_0819.md
---
# AF-0667 - Surviving mutant: a TLSA query name of exactly 255 octets can be refused as too long

## Summary

Medium finding from the quality auditor at `Curl.Extension.Dane.UnitLibrary/Wire/DnssecQueryEncoder.cs:80`: Surviving mutant: a TLSA query name of exactly 255 octets can be refused as too long. Reported by an auditor flagged unreliable in 2026-11-04_1442.md.

## Evidence

Location: `Curl.Extension.Dane.UnitLibrary/Wire/DnssecQueryEncoder.cs:80`

Mutant 'wire.Count > MaximumNameLength' -> 'wire.Count >= MaximumNameLength' survived (sample -Seed 413592). RFC 1035 allows 255 octets. The tests check only a name over the limit (Encode_Rfc1035Section2_3_4_ANameOver255OctetsIsRefused uses 4x63-octet labels, 257 octets) and never a name of exactly 255. The mutant makes DANE refuse a valid host at the limit, which ends the transfer.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Dane.UnitLibrary/Wire/DnssecQueryEncoder.cs:80:> -Member EncodeName -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-05 | 2026-11-05_0819.md | reproduces: no | Ran the -Site command: 'wire.Count > MaximumNameLength' -> '>=' at line 80, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-04: filed proposed.
- 2026-11-05: accepted -> closed. Re-audit 2026-11-05_0819.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
