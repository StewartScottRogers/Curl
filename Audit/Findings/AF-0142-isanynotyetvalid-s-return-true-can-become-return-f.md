---
id: AF-0142
title: IsAnyNotYetValid's 'return true' can become 'return false' with no test failing: not-yet-valid certificates report 'expired (10)'
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs:IsAnyNotYetValid-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs:131:true
task: none
tasks:
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0142 - IsAnyNotYetValid's 'return true' can become 'return false' with no test failing: not-yet-valid certificates report 'expired (10)'

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs:131`: IsAnyNotYetValid's 'return true' can become 'return false' with no test failing: not-yet-valid certificates report 'expired (10)'.

## Evidence

Location: `Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs:131`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: survived, original 'return true;' mutated 'return false;' in IsAnyNotYetValid. OpenSslVerifyResult.Of passes IsAnyNotYetValid(chain, now) to OfChainStatus, which picks CertificateNotYetValid (9) over CertificateHasExpired (10) only when it is true; ServerCertificateVerification.cs:281 uses Of for the verify result curl's OpenSSL build reports. With the mutant every NotTimeValid chain is reported as expired, so a certificate whose NotBefore is in the future gets 'certificate has expired (10)' instead of 'certificate is not yet valid (9)' in -v/-k output. OpenSslVerifyResultTests covers OfChainStatus with the flag given directly (DataRow at line 102) but no test drives Of with a not-yet-valid chain.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs:131:true -Member IsAnyNotYetValid -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-10: filed proposed.
- 2026-10-10: proposed -> accepted.
