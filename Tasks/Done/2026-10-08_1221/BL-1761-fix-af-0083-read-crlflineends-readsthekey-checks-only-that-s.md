---
id: BL-1761
title: Fix AF-0083: Read_CrLfLineEnds_ReadsTheKey checks only that some key came back, not that it is the RSA key in the file
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1761 — Fix AF-0083: Read_CrLfLineEnds_ReadsTheKey checks only that some key came back, not that it is the RSA key in the file

## Goal

The defect the audit office reported as AF-0083 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0083 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0083-read-crlflineends-readsthekey-checks-only-that-som.md`.

Location: `Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs:303`

Location: `Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs:303`

The only assertion is 'Assert.IsNotNull(key);'. If a CR were left in the base64 or DER and the key came out corrupted but non-null, the test would still pass. It does not compare key.PublicKey.Blob with TestUserKeys' RSA public key, as SshUserKeySourceTests does.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ssh.UnitTests/Keys/SshPrivateKeyReaderTests.cs -Pattern 'Read_CrLfLineEnds_ReadsTheKey' -Context 0,10
```

- Expected: The body compares the key's public blob or type with the known RSA key.
- Actual: SshPrivateKeyReaderTests.cs:303: Assert.IsNotNull(key);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now asserts what `Read_RsaKeyInEachFormat_ReadsTheKeyOfItsPublicKeyFile` asserts: an `RsaSshPrivateKey` of type `ssh-rsa` whose public blob equals `TestUserKeys.RsaPublicKeyFile`'s. A CR left in the base64 or DER would now change the blob and fail it. Name kept, so the finding's reproduction still finds it.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. CRLF test now compares the RSA public key blob and type with TestUserKeys.RsaPublicKeyFile
