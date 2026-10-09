---
id: BL-1872
title: Fix AF-0122: GetInitialTicketAsync_MethodDataWithoutKeyInfo_UsesTheFirstTypeAndDefaultSalt never checks which encryption type was used
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Kerberos.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1872 — Fix AF-0122: GetInitialTicketAsync_MethodDataWithoutKeyInfo_UsesTheFirstTypeAndDefaultSalt never checks which encryption type was used

## Goal

The defect the audit office reported as AF-0122 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0122 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0122-getinitialticketasync-methoddatawithoutkeyinfo-use.md`.

Location: `Curl.Kerberos.UnitTests/KerberosKdcClientTests.cs:251`

Location: `Curl.Kerberos.UnitTests/KerberosKdcClientTests.cs:251`

The only assertion is 'Assert.IsNotNull(kdc.LastTimestamp);'. FakeKdc (FakeKdc.cs:193-198) decrypts the timestamp with ClientKey(encrypted.EncryptionType, Salt) for whatever type the client picked, so a non-null timestamp shows the default salt was used but not that the first type was chosen. A client that picked the second offered type would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Kerberos.UnitTests/KerberosKdcClientTests.cs -Pattern 'UsesTheFirstTypeAndDefaultSalt' -Context 0,7
```

- Expected: An assertion on the encryption type of the encrypted timestamp as well as its decryption.
- Actual: Assert.IsNotNull(kdc.LastTimestamp); is the test's only assertion.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
