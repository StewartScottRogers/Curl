---
id: AF-0421
title: CertificateRevocationList.Revokes' isUnsigned: false can become true with no test failing, so --crlfile misses a revoked certificate whose serial number is negative
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-24_2106.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/CertificateRevocationList.cs:Revokes-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/CertificateRevocationList.cs:137:false
task: BL-3470
tasks: BL-3470
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed: 2026-10-24
closed-how: mechanical
closed-by: 2026-10-24_2106.md
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

- 2026-10-24 | 2026-10-24_2106.md | reproduces: no | Ran the -Site command: CertificateRevocationList.cs:137 Revokes 'isUnsigned: false' -> 'isUnsigned: true', resolvedLine 137, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> accepted.
- 2026-10-24: accepted -> closed. Re-audit 2026-10-24_2106.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
