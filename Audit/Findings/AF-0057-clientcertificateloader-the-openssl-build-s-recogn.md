---
id: AF-0057
title: ClientCertificateLoader: the OpenSSL build's recognisesDriveLetters: false can become true with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:static-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:74:false
task: BL-1700
tasks: BL-1700
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed: 2026-10-08
closed-how: mechanical
closed-by: 2026-10-08_0748.md
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

- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | Ran the -Site command: killed at ClientCertificateLoader.cs:74 ('recognisesDriveLetters: false' -> true). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
- 2026-10-08: accepted -> closed. Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
