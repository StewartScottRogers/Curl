---
id: AF-0765
title: Surviving mutant: --key PEM reading can try to decrypt a non-key block (or decrypt without a passphrase)
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:PemPrivateKeyInfo-and:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:444:&&
task: none
tasks:
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0765 - Surviving mutant: --key PEM reading can try to decrypt a non-key block (or decrypt without a passphrase)

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:444`: Surviving mutant: --key PEM reading can try to decrypt a non-key block (or decrypt without a passphrase). Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:444`

Mutant 'if (passphrase is not null && label.SequenceEqual(EncryptedPrivateKeyLabel))' -> '||' survived (seed 799365 sample). With a passphrase, the mutant decrypts the first PEM block whatever its label: a combined file with the CERTIFICATE block first and an ENCRYPTED PRIVATE KEY after it, a common --cert/--key layout with --pass, fails instead of loading. Without a passphrase, an ENCRYPTED PRIVATE KEY block is passed to Decrypt(..., null) instead of giving 'The key file holds no PEM PRIVATE KEY.'. Both change the transfer's exit code and message.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:444:&& -Member PemPrivateKeyInfo -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-14: filed proposed.
