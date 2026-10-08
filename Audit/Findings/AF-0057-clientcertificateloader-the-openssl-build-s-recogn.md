---
id: AF-0057
title: ClientCertificateLoader: the OpenSSL build's recognisesDriveLetters: false can become true with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:static-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:74:false
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
# AF-0057 - ClientCertificateLoader: the OpenSSL build's recognisesDriveLetters: false can become true with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:74`: ClientCertificateLoader: the OpenSSL build's recognisesDriveLetters: false can become true with no test failing.

## Evidence

Location: `Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:74`

Mutant: return LoadAsOpenSslBuild(options, recognisesDriveLetters: false); -> recognisesDriveLetters: true. It survived the sampled run (seed 0). This flag decides how --cert splits 'file:passphrase'. On the OpenSSL build a value like 'C:cert.pem' or 'x:pass' would be split differently: a different file is loaded, or the passphrase is lost. That decides whether the transfer fails with exit 58. No test pins how the OpenSSL build splits a drive-letter-like --cert.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:74:false -Member static -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
