---
id: BL-1680
title: Fix AF-0053: ReadPrivateKeyAsync_Passphrase_EncodedWithTheCredentialEncoding uses an ASCII passphrase and only IsNotNull, so it cannot tell which encoding was used
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1680 — Fix AF-0053: ReadPrivateKeyAsync_Passphrase_EncodedWithTheCredentialEncoding uses an ASCII passphrase and only IsNotNull, so it cannot tell which encoding was used

## Goal

The defect the audit office reported as AF-0053 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0053 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0053-readprivatekeyasync-passphrase-encodedwiththecrede.md`.

Location: `Curl.Protocol.Ssh.UnitTests/Keys/SshUserKeySourceTests.cs:80`

Location: `Curl.Protocol.Ssh.UnitTests/Keys/SshUserKeySourceTests.cs:80`

The body decrypts TestUserKeys.RsaPkcs1Aes128 with TestUserKeys.Passphrase = "secret" (Fakes/TestUserKeys.cs:13), passes Encoding.UTF8 as the credential encoding (line 134), and asserts only Assert.IsNotNull(key). "secret" is the same bytes in ASCII, Latin-1, UTF-8 and every ANSI code page, so replacing passphraseEncoding.GetBytes (SshUserKeySource.cs:82) with any other encoding still passes. The name promises the credential encoding is used; the test cannot detect otherwise.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ssh.UnitTests/Keys/SshUserKeySourceTests.cs,Curl.Protocol.Ssh.UnitTests/Fakes/TestUserKeys.cs -Pattern 'EncodedWithTheCredentialEncoding|Assert.IsNotNull\(key\)|const string Passphrase'
```

- Expected: A non-ASCII passphrase whose bytes differ by encoding, and an assertion that only the credential encoding decrypts the key.
- Actual: Passphrase = "secret" (pure ASCII) and the only check is Assert.IsNotNull(key).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
