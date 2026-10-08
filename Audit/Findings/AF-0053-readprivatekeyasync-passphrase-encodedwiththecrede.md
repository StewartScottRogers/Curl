---
id: AF-0053
title: ReadPrivateKeyAsync_Passphrase_EncodedWithTheCredentialEncoding uses an ASCII passphrase and only IsNotNull, so it cannot tell which encoding was used
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces.
key: quality:Curl.Protocol.Ssh.UnitTests/Keys/SshUserKeySourceTests.cs:ReadPrivateKeyAsync_Passphrase_EncodedWithTheCredentialEncoding:name-lies
reproduction: none
task: BL-1680
tasks: BL-1680
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed: 2026-10-08
closed-how: reliable-reaudit
closed-by: 2026-10-08_0748.md
---
# AF-0053 - ReadPrivateKeyAsync_Passphrase_EncodedWithTheCredentialEncoding uses an ASCII passphrase and only IsNotNull, so it cannot tell which encoding was used

## Summary

Medium finding from the quality auditor at `Curl.Protocol.Ssh.UnitTests/Keys/SshUserKeySourceTests.cs:80`: ReadPrivateKeyAsync_Passphrase_EncodedWithTheCredentialEncoding uses an ASCII passphrase and only IsNotNull, so it cannot tell which encoding was used.

## Evidence

Location: `Curl.Protocol.Ssh.UnitTests/Keys/SshUserKeySourceTests.cs:80`

The body decrypts TestUserKeys.RsaPkcs1Aes128 with TestUserKeys.Passphrase = "secret" (Fakes/TestUserKeys.cs:13), passes Encoding.UTF8 as the credential encoding (line 134), and asserts only Assert.IsNotNull(key). "secret" is the same bytes in ASCII, Latin-1, UTF-8 and every ANSI code page, so replacing passphraseEncoding.GetBytes (SshUserKeySource.cs:82) with any other encoding still passes. The name promises the credential encoding is used; the test cannot detect otherwise.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ssh.UnitTests/Keys/SshUserKeySourceTests.cs,Curl.Protocol.Ssh.UnitTests/Fakes/TestUserKeys.cs -Pattern 'EncodedWithTheCredentialEncoding|Assert.IsNotNull\(key\)|const string Passphrase'
```

- Expected: A non-ASCII passphrase whose bytes differ by encoding, and an assertion that only the credential encoding decrypts the key.
- Actual: Passphrase = "secret" (pure ASCII) and the only check is Assert.IsNotNull(key).

## Re-audits

- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | Ran the Select-String. The test now uses TestUserKeys.PassphraseWhoseUtf16BytesAreSecret with a UTF-16 (Encoding.Unicode) source. After Assert.IsNotNull(key) it does CollectionAssert.AreEqual(expected, key.PublicKey.Blob) (SshUserKeySourceTests.cs:111-125), so the wrong encoding would fail.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
- 2026-10-08: accepted -> closed. Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces.
