---
id: AF-0122
title: GetInitialTicketAsync_MethodDataWithoutKeyInfo_UsesTheFirstTypeAndDefaultSalt never checks which encryption type was used
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Kerberos.UnitTests/KerberosKdcClientTests.cs:GetInitialTicketAsync_MethodDataWithoutKeyInfo_UsesTheFirstTypeAndDefaultSalt:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0122 - GetInitialTicketAsync_MethodDataWithoutKeyInfo_UsesTheFirstTypeAndDefaultSalt never checks which encryption type was used

## Summary

Low finding from the quality auditor at `Curl.Kerberos.UnitTests/KerberosKdcClientTests.cs:251`: GetInitialTicketAsync_MethodDataWithoutKeyInfo_UsesTheFirstTypeAndDefaultSalt never checks which encryption type was used. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Kerberos.UnitTests/KerberosKdcClientTests.cs:251`

The only assertion is 'Assert.IsNotNull(kdc.LastTimestamp);'. FakeKdc (FakeKdc.cs:193-198) decrypts the timestamp with ClientKey(encrypted.EncryptionType, Salt) for whatever type the client picked, so a non-null timestamp shows the default salt was used but not that the first type was chosen. A client that picked the second offered type would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Kerberos.UnitTests/KerberosKdcClientTests.cs -Pattern 'UsesTheFirstTypeAndDefaultSalt' -Context 0,7
```

- Expected: An assertion on the encryption type of the encrypted timestamp as well as its decryption.
- Actual: Assert.IsNotNull(kdc.LastTimestamp); is the test's only assertion.

## Re-audits

## Log

- 2026-10-08: filed proposed.
