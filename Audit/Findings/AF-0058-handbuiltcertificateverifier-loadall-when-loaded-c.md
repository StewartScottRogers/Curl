---
id: AF-0058
title: HandBuiltCertificateVerifier.LoadAll: 'when (loaded.Count > 0)' can become >= 0 with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs:LoadAll-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs:82:>
task: none
tasks:
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0058 - HandBuiltCertificateVerifier.LoadAll: 'when (loaded.Count > 0)' can become >= 0 with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs:82`: HandBuiltCertificateVerifier.LoadAll: 'when (loaded.Count > 0)' can become >= 0 with no test failing.

## Evidence

Location: `Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs:82`

Mutant: catch (CryptographicException) when (loaded.Count > 0) -> when (loaded.Count >= 0). It survived the sampled run (seed 0). The comment says the server's own certificate failing to parse means 'none, as though it sent nothing'. With the mutant, an unparseable leaf is skipped and the next chain candidate becomes the first certificate. That changes which certificate the hand-built TLS stack verifies the host against, a trust decision. No test sends an unparseable leaf followed by a valid certificate.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs:82:> -Member LoadAll -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
