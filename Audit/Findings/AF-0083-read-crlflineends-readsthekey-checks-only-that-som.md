---
id: AF-0083
title: Read_CrLfLineEnds_ReadsTheKey checks only that some key came back, not that it is the RSA key in the file
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs:Read_CrLfLineEnds_ReadsTheKey:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-10-08: filed proposed.
