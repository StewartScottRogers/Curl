---
id: AF-0158
title: KerberosFileError.UnsupportedType says only FILE: or DIR: caches can store a credential; CredentialCacheStore.Store also stores into KCM:
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Curl.Kerberos.UnitLibrary/KerberosFileError.cs:UnsupportedType:false-doc-comment
reproduction: none
task: BL-2051
tasks: BL-2051
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0158 - KerberosFileError.UnsupportedType says only FILE: or DIR: caches can store a credential; CredentialCacheStore.Store also stores into KCM:

## Summary

Low finding from the truthfulness auditor at `Curl.Kerberos.UnitLibrary/KerberosFileError.cs:19`: KerberosFileError.UnsupportedType says only FILE: or DIR: caches can store a credential; CredentialCacheStore.Store also stores into KCM:. Reported by an auditor flagged unreliable in 2026-10-11_0051.md.

## Evidence

Location: `Curl.Kerberos.UnitLibrary/KerberosFileError.cs:19`

KerberosFileError.cs:18-24 documents UnsupportedType as a name type prefix that is '... anything but FILE: or DIR: for storing a credential in one ...'. CredentialCacheStore.Store (CredentialCacheStore.cs:135-148) stores a KCM: credential: `if (parsed.Type == KcmType) { StoreMarshalled(credential, bytes => StoreKcmCredential(parsed.Residual, bytes)); return; }`, and Store's own doc (lines 121-133) lists KCM: as supported. The enum's doc comment says KCM: storing is unsupported, which is false. Found in the seeded name sample (type KerberosFileError).

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Kerberos.UnitLibrary/KerberosFileError.cs -Pattern 'for storing a credential in one').Line.Trim(); (Select-String -Path Curl.Kerberos.UnitLibrary/CredentialCacheStore.cs -Pattern 'StoreKcmCredential\(parsed').Line.Trim()
```

- Expected: The UnsupportedType doc lists FILE:, DIR: and KCM: as the types a credential can be stored in
- Actual: '/// <c>DIR:</c> for storing a credential in one, and anything but <c>FILE:</c> or' beside 'StoreMarshalled(credential, bytes => StoreKcmCredential(parsed.Residual, bytes));'

## Re-audits

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
