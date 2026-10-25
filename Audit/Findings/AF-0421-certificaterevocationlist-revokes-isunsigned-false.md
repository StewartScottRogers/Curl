---
id: AF-0421
title: CertificateRevocationList.Revokes' isUnsigned: false can become true with no test failing, so --crlfile misses a revoked certificate whose serial number is negative
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/CertificateRevocationList.cs:Revokes-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/CertificateRevocationList.cs:137:false
task: none
tasks:
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0421 - CertificateRevocationList.Revokes' isUnsigned: false can become true with no test failing, so --crlfile misses a revoked certificate whose serial number is negative

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/CertificateRevocationList.cs:137`: CertificateRevocationList.Revokes' isUnsigned: false can become true with no test failing, so --crlfile misses a revoked certificate whose serial number is negative.

## Evidence

Location: `Curl.Networking.UnitLibrary/CertificateRevocationList.cs:137`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: survived, '_revokedSerialNumbers.Contains(new BigInteger(certificate.SerialNumberBytes.Span, isUnsigned: false, isBigEndian: true));' -> 'isUnsigned: true'. The CRL's serials are read as signed DER INTEGERs (ReadRevokedSerialNumbers, revoked.ReadSequence().ReadInteger()). Reading the certificate's serial unsigned changes its value whenever the first byte is 0x80 or above, so a listed certificate with a negative serial passes. The transfer would succeed instead of failing with exit 60. The tests' certificates have no such serial.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/CertificateRevocationList.cs:137:false -Member Revokes -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> accepted.
