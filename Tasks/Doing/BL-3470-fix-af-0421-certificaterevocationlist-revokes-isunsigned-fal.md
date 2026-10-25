---
id: BL-3470
title: Fix AF-0421: CertificateRevocationList.Revokes' isUnsigned: false can become true with no test failing, so --crlfile misses a revoked certificate whose serial number is negative
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-24
completed:
---
# BL-3470 — Fix AF-0421: CertificateRevocationList.Revokes' isUnsigned: false can become true with no test failing, so --crlfile misses a revoked certificate whose serial number is negative

## Goal

The defect the audit office reported as AF-0421 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0421 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0421-certificaterevocationlist-revokes-isunsigned-false.md`.

Location: `Curl.Networking.UnitLibrary/CertificateRevocationList.cs:137`

Location: `Curl.Networking.UnitLibrary/CertificateRevocationList.cs:137`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: survived, '_revokedSerialNumbers.Contains(new BigInteger(certificate.SerialNumberBytes.Span, isUnsigned: false, isBigEndian: true));' -> 'isUnsigned: true'. The CRL's serials are read as signed DER INTEGERs (ReadRevokedSerialNumbers, revoked.ReadSequence().ReadInteger()). Reading the certificate's serial unsigned changes its value whenever the first byte is 0x80 or above, so a listed certificate with a negative serial passes. The transfer would succeed instead of failing with exit 60. The tests' certificates have no such serial.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/CertificateRevocationList.cs:137:false -Member Revokes -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
