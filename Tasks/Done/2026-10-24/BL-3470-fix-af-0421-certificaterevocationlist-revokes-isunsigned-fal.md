---
id: BL-3470
title: Fix AF-0421: CertificateRevocationList.Revokes' isUnsigned: false can become true with no test failing, so --crlfile misses a revoked certificate whose serial number is negative
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/CertificateRevocationListTests.cs]
requirement: none
created: 2026-10-24
completed: 2026-10-24
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Narrowed nothing; added `Curl.Networking.UnitTests/CertificateRevocationListTests.cs` to touches for the test (no Doing task on origin/work/dark-factory names it).
- The fix is a test, `Revokes_WithACertificateWhoseSerialNumberIsNegative_MatchesItsSignedListEntry`: production code is unchanged and already right. `CertificateRequest.Create` always encodes a serial as positive (it prefixes 00), so the test issues a leaf with serial 00 9A 01 and patches its DER to the same-length negative INTEGER 9A 01 02. It then checks that a list `CertificateRevocationListBuilder` writes from those bytes revokes it, and that a list holding the positive 00 9A 01 02 does not. Under `isUnsigned: true` both checks flip.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so I checked the reproduction by hand: with line 137 changed to `isUnsigned: true`, the new test failed (killed); reverted, all 13 `CertificateRevocationListTests` pass.
- Platform: the patched certificate is loaded with `X509CertificateLoader.LoadCertificate`, and the test never checks its signature; OpenSSL and Apple both accept negative serial numbers, so the test is platform-neutral.
- dotnet build clean; fast tests: 46 projects passed, 0 failed. No production change, so no Measure-CodeQuality run was needed.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-24: Doing -> Done. CertificateRevocationList.Revokes has a test with a negative certificate serial number, which kills the isUnsigned mutant (AF-0421)
