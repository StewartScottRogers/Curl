---
id: BL-2051
title: Fix AF-0158: KerberosFileError.UnsupportedType says only FILE: or DIR: caches can store a credential; CredentialCacheStore.Store also stores into KCM:
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Kerberos.UnitLibrary]
requirement: none
created: 2026-10-11
completed:
---
# BL-2051 — Fix AF-0158: KerberosFileError.UnsupportedType says only FILE: or DIR: caches can store a credential; CredentialCacheStore.Store also stores into KCM:

## Goal

The defect the audit office reported as AF-0158 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0158 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0158-kerberosfileerror-unsupportedtype-says-only-file-o.md`.

Location: `Curl.Kerberos.UnitLibrary/KerberosFileError.cs:19`

Location: `Curl.Kerberos.UnitLibrary/KerberosFileError.cs:19`

KerberosFileError.cs:18-24 documents UnsupportedType as a name type prefix that is '... anything but FILE: or DIR: for storing a credential in one ...'. CredentialCacheStore.Store (CredentialCacheStore.cs:135-148) stores a KCM: credential: `if (parsed.Type == KcmType) { StoreMarshalled(credential, bytes => StoreKcmCredential(parsed.Residual, bytes)); return; }`, and Store's own doc (lines 121-133) lists KCM: as supported. The enum's doc comment says KCM: storing is unsupported, which is false. Found in the seeded name sample (type KerberosFileError).

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Kerberos.UnitLibrary/KerberosFileError.cs -Pattern 'for storing a credential in one').Line.Trim(); (Select-String -Path Curl.Kerberos.UnitLibrary/CredentialCacheStore.cs -Pattern 'StoreKcmCredential\(parsed').Line.Trim()
```

- Expected: The UnsupportedType doc lists FILE:, DIR: and KCM: as the types a credential can be stored in
- Actual: '/// <c>DIR:</c> for storing a credential in one, and anything but <c>FILE:</c> or' beside 'StoreMarshalled(credential, bytes => StoreKcmCredential(parsed.Residual, bytes));'

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-11: Created.
