---
id: AF-0122
title: GetInitialTicketAsync_MethodDataWithoutKeyInfo_UsesTheFirstTypeAndDefaultSalt never checks which encryption type was used
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-09_1435.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0647.md, 2026-10-09_1435.md).
key: quality:Curl.Kerberos.UnitTests/KerberosKdcClientTests.cs:GetInitialTicketAsync_MethodDataWithoutKeyInfo_UsesTheFirstTypeAndDefaultSalt:weak-assertion
reproduction: none
task: BL-1872
tasks: BL-1872
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed: 2026-10-09
closed-how: consecutive
closed-by: 2026-10-09_0647.md, 2026-10-09_1435.md
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

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Select-String shows KerberosKdcClientTests.cs:245-252: GetInitialTicketAsync_MethodDataWithoutKeyInfo_UsesTheFirstTypeAndDefaultSalt's only assertion is Assert.IsNotNull(kdc.LastTimestamp). It never checks the encryption type or the salt.
- 2026-10-09 | 2026-10-09_0647.md | reproduces: no | Ran the Select-String reproduction: the test now asserts Assert.IsGreaterThan(1, offeredTypes.Count) and Assert.AreEqual(offeredTypes[0], KerberosEncryptedData.Decode(kdc.Requests[1].PreAuthenticationData.Single().Value).EncryptionType), so it checks that the first offered type was used (KerberosKdcClientTests.cs:251-253).
- 2026-10-09 | 2026-10-09_1435.md | reproduces: no | Ran the Select-String: GetInitialTicketAsync_MethodDataWithoutKeyInfo_UsesTheFirstTypeAndDefaultSalt now asserts Assert.IsGreaterThan(1, offeredTypes.Count) and Assert.AreEqual(offeredTypes[0], KerberosEncryptedData.Decode(...PreAuthenticationData.Single().Value).EncryptionType), so it checks which encryption type was used.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_1435.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0647.md, 2026-10-09_1435.md).
