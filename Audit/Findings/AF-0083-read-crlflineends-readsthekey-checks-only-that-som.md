---
id: AF-0083
title: Read_CrLfLineEnds_ReadsTheKey checks only that some key came back, not that it is the RSA key in the file
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-09_0225.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_2315.md, 2026-10-09_0225.md).
key: quality:Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs:Read_CrLfLineEnds_ReadsTheKey:weak-assertion
reproduction: none
task: BL-1761
tasks: BL-1761
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: consecutive
closed-by: 2026-10-08_2315.md, 2026-10-09_0225.md
---
# AF-0083 - Read_CrLfLineEnds_ReadsTheKey checks only that some key came back, not that it is the RSA key in the file

## Summary

Low finding from the quality auditor at `Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs:303`: Read_CrLfLineEnds_ReadsTheKey checks only that some key came back, not that it is the RSA key in the file.

## Evidence

Location: `Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs:303`

The only assertion is 'Assert.IsNotNull(key);'. If a CR were left in the base64 or DER and the key came out corrupted but non-null, the test would still pass. It does not compare key.PublicKey.Blob with TestUserKeys' RSA public key, as SshUserKeySourceTests does.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs -Pattern 'Read_CrLfLineEnds_ReadsTheKey' -Context 0,10
```

- Expected: The body compares the key's public blob or type with the known RSA key.
- Actual: SshPrivateKeyReaderTests.cs:303: Assert.IsNotNull(key);

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | reproduces: no | Ran the Select-String: Read_CrLfLineEnds_ReadsTheKey now takes expected = SshPublicKeyFile.Parse(TestUserKeys.RsaPublicKeyFile).Key!.Blob and asserts the public key blob (line 303) and Assert.IsInstanceOfType<RsaSshPrivateKey>(key) (line 304).
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | Read_CrLfLineEnds_ReadsTheKey (SshPrivateKeyReaderTests.cs:294) asserts IsInstanceOfType<RsaSshPrivateKey>, KeyType "ssh-rsa" and CollectionAssert.AreEqual of the public key blob against TestUserKeys.RsaPublicKeyFile.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_2315.md, 2026-10-09_0225.md).
