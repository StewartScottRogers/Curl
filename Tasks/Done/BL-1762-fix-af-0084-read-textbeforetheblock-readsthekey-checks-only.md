---
id: BL-1762
title: Fix AF-0084: Read_TextBeforeTheBlock_ReadsTheKey checks only that some key came back, not that it is the ECDSA P-256 key in the file
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1762 — Fix AF-0084: Read_TextBeforeTheBlock_ReadsTheKey checks only that some key came back, not that it is the ECDSA P-256 key in the file

## Goal

The defect the audit office reported as AF-0084 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0084 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0084-read-textbeforetheblock-readsthekey-checks-only-th.md`.

Location: `Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs:316`

Location: `Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs:316`

The only assertion is 'Assert.IsNotNull(key);', after prefixing 'Bag Attributes' to TestUserKeys.EcdsaP256Sec1. Neither the key type nor the public key is checked, so a reader that took a wrong or partial key would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs -Pattern 'Read_TextBeforeTheBlock_ReadsTheKey' -Context 0,10
```

- Expected: The body compares the key's type and public blob with the known ECDSA P-256 key.
- Actual: SshPrivateKeyReaderTests.cs:316: Assert.IsNotNull(key);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now asserts the key is an `EcdsaSshPrivateKey` of type `ecdsa-sha2-nistp256` whose public key blob equals the blob in `TestUserKeys.EcdsaP256PublicKeyFile`, the same checks as `Read_EcdsaKeyInEachFormat_ReadsTheKeyOfItsPublicKeyFile`.
- Kept the method name `Read_TextBeforeTheBlock_ReadsTheKey`: it is still true ("the key" is the file's key), and the finding's `Select-String` reproduction searches for it, so a rename would leave the re-audit nothing to find.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Test now asserts the ECDSA P-256 key type and public key blob
