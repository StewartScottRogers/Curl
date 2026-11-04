---
id: AF-0671
title: Surviving mutant: a TLS handshake that fails before the certificate can be reported as certificate-verified
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/PeerVerification.cs:Unobserved-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/PeerVerification.cs:15:false
task: none
tasks:
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0671 - Surviving mutant: a TLS handshake that fails before the certificate can be reported as certificate-verified

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/PeerVerification.cs:15`: Surviving mutant: a TLS handshake that fails before the certificate can be reported as certificate-verified. Reported by an auditor flagged unreliable in 2026-11-04_1442.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/PeerVerification.cs:15`

Mutant 'Unobserved { get; } = new(false, null, []);' -> 'new(true, null, [])' survived (sample -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 413592). SslStreamTlsProvider starts with peerVerification = PeerVerification.Unobserved (line 435) and passes it to ReportFailedHandshake when the handshake fails before RemoteCertificateValidationCallback runs (line 496). There CertificateVerified = peerVerification.Verified (line 662), so the handshake event claims a verified certificate that was never seen. HandBuiltCertificateVerifier.Observed defaults to it as well.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/PeerVerification.cs:15:false -Member Unobserved -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
