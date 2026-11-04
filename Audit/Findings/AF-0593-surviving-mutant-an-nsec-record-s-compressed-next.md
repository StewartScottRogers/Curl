---
id: AF-0593
title: Surviving mutant: an NSEC record's compressed next name can be accepted without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.Dane.UnitLibrary/Wire/DnsRecordDataDecoder.cs:DecodeNsec-false:surviving-mutant
reproduction: mutation Curl.Extension.Dane.UnitLibrary/Wire/DnsRecordDataDecoder.cs:74:false
task: BL-4757
tasks: BL-4757
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1615.md
---
# AF-0593 - Surviving mutant: an NSEC record's compressed next name can be accepted without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Extension.Dane.UnitLibrary/Wire/DnsRecordDataDecoder.cs:74`: Surviving mutant: an NSEC record's compressed next name can be accepted without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.Dane.UnitLibrary/Wire/DnsRecordDataDecoder.cs:74`

Drawn library. reader.ReadName(allowCompression: false) -> true survived in DecodeNsec: RFC 4034 section 4.1.1 forbids compressing the Next Domain Name, and the decoder's refusal of it is untested (only RRSIG's signer has TryDecode_Rfc4034Section3_1_7_ACompressedSignerNameGivesFalse). A malformed NSEC then feeds the DANE denial-of-existence proofs.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Dane.UnitLibrary/Wire/DnsRecordDataDecoder.cs:74:false -Member DecodeNsec -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the -Site command: DnsRecordDataDecoder.cs:74 false resolved to line 74, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
