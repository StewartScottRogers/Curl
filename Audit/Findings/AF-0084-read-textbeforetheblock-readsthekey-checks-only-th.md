---
id: AF-0084
title: Read_TextBeforeTheBlock_ReadsTheKey checks only that some key came back, not that it is the ECDSA P-256 key in the file
auditor: quality
severity: Low
status: accepted
reason:
key: quality:Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs:Read_TextBeforeTheBlock_ReadsTheKey:weak-assertion
reproduction: none
task: BL-1762
tasks: BL-1762
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0084 - Read_TextBeforeTheBlock_ReadsTheKey checks only that some key came back, not that it is the ECDSA P-256 key in the file

## Summary

Low finding from the quality auditor at `Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs:316`: Read_TextBeforeTheBlock_ReadsTheKey checks only that some key came back, not that it is the ECDSA P-256 key in the file.

## Evidence

Location: `Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs:316`

The only assertion is 'Assert.IsNotNull(key);', after prefixing 'Bag Attributes' to TestUserKeys.EcdsaP256Sec1. Neither the key type nor the public key is checked, so a reader that took a wrong or partial key would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs -Pattern 'Read_TextBeforeTheBlock_ReadsTheKey' -Context 0,10
```

- Expected: The body compares the key's type and public blob with the known ECDSA P-256 key.
- Actual: SshPrivateKeyReaderTests.cs:316: Assert.IsNotNull(key);

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | reproduces: no | Ran the Select-String: Read_TextBeforeTheBlock_ReadsTheKey now asserts the key type (line 319) and public key blob (line 320) against SshPublicKeyFile.Parse(TestUserKeys.EcdsaP256PublicKeyFile).Key.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
