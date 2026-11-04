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
completed: 2026-11-03
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `TryDecode_Rfc4034Section4_1_1_ACompressedNsecNextNameGivesFalse` to `Curl.Extension.Dane.UnitTests`. The NSEC RDATA is decoded from a reader over the RDATA alone, so a pointer counts from the RDATA's first octet and must point backwards. The test's next name is therefore a one-octet label holding 0x00 and then a pointer back to that octet (the root), which decodes when compression is allowed. A first draft that reused the message-relative pointer `C0 0C` let the mutant survive, because that pointer landed in the type bit maps and failed for another reason.
- The lane may not run the audit office's mutation tool, so the mutant was checked by hand. With `allowCompression: true` at `DnsRecordDataDecoder.cs:74` the new test fails (killed). With production restored it passes. There is no production change, so no quality run was needed. `dotnet build` is clean (0 warnings) and the fast tests are green. The re-audit closes the finding.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Test kills the compressed NSEC next name mutant
