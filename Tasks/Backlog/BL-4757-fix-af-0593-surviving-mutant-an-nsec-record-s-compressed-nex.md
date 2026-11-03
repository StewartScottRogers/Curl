---
id: BL-4757
title: Fix AF-0593: Surviving mutant: an NSEC record's compressed next name can be accepted without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Dane.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4757 — Fix AF-0593: Surviving mutant: an NSEC record's compressed next name can be accepted without a test failing

## Goal

The defect the audit office reported as AF-0593 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0593 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0593-surviving-mutant-an-nsec-record-s-compressed-next.md`.

Location: `Curl.Extension.Dane.UnitLibrary/Wire/DnsRecordDataDecoder.cs:74`

Location: `Curl.Extension.Dane.UnitLibrary/Wire/DnsRecordDataDecoder.cs:74`

Drawn library. reader.ReadName(allowCompression: false) -> true survived in DecodeNsec: RFC 4034 section 4.1.1 forbids compressing the Next Domain Name, and the decoder's refusal of it is untested (only RRSIG's signer has TryDecode_Rfc4034Section3_1_7_ACompressedSignerNameGivesFalse). A malformed NSEC then feeds the DANE denial-of-existence proofs.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Dane.UnitLibrary/Wire/DnsRecordDataDecoder.cs:74:false -Member DecodeNsec -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
