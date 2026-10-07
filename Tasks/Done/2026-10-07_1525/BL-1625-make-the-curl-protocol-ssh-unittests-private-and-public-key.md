---
id: BL-1625
title: Make the Curl.Protocol.Ssh.UnitTests private and public key tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1625 — Make the Curl.Protocol.Ssh.UnitTests private and public key tests write descriptive diagnostic output

## Goal

Every test in these files of `Curl.Protocol.Ssh.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases; no test's logic or assertions change. Files: Keys/ (about 85 test methods, counted 2026-10-07).

## Context

- Split from BL-1484: one task for the whole project (811 test methods in 94 files) was too big for one `/task-run`. BL-1484's Context applies in full: the line format and helper usage in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; what matters for SSH (packets as `BYTES` with their decoded message number, negotiated algorithms, keys as hex, authentication steps, SFTP and SCP requests and replies, `PHASE` lines); output only; `BYTES` for large payloads; nothing OS-dependent. BL-1481 (`Curl.Protocol.Rtsp.UnitTests/RtspDiagnostics.cs`) is a worked example.
- A shared helper in this project (for example `SshDiagnostics.cs`) may be added or extended by whichever split task gets there first; the others reuse it.
- Classes: Asn1PrivateKeyDecoderTests, DsaSshPrivateKeyTests, EcdsaSshPrivateKeyTests, Ed25519SshPrivateKeyTests, KeyFileCbcDecryptionTests, LegacyPemDecryptionTests, OpenSshPrivateKeyDecoderTests, PemBlockTests, Pkcs8DecryptionTests, RsaSshPrivateKeyTests, SshPrivateKeyReaderTests, SshPublicKeyFileTests, SshUserKeySourceTests.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration&(FullyQualifiedName~<class>|...)" --logger "console;verbosity=detailed"` over this task's classes prints an `END` line for every test it runs, and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing.
- [x] The filtered run's test count is unchanged, and in this task's files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (`Select-String -AllMatches`) are no lower than before; before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Ssh.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Helper: new `Curl.Protocol.Ssh.UnitTests/Keys/SshKeyDiagnostics.cs` (keys with their type and public key blob as `BYTES`, public key readings, caught exceptions with type `ASSERT`, byte arrays as hex `ASSERT` plus `DIFF`). It reuses `SshAuthenticationDiagnostics.Text` (BL-1622) to escape key file text onto one line. Exception tests keep their `Assert.ThrowsExactly`/`Assert.Throws` and now write what they caught; a test that only asserted a call's result in one expression now holds the result in a local first, so the logic and assertions are unchanged.
- Counts in the 13 task files, before -> after (`Select-String -AllMatches`): `Assert.` 123 -> 123, `[TestMethod` 85 -> 85, `[DataRow(` 119 -> 119. Filtered run: 176 tests before and after, all passed; no `END` line with arrange, act or assert 0. Whole project fast run: 1673 passed.
- `SLOW:` lines (2026-10-07, other lanes building): every one is the bcrypt-pbkdf key derivation of an OpenSSH-encrypted key file (16 rounds, as `ssh-keygen` writes), in its `PHASE`:
  - `SshPrivateKeyReaderTests.Read_Ed25519KeyEncryptedWithEachCipher_ReadsTheKeyOfItsPublicKeyFile`, all nine rows: `PHASE read and decrypt` 8499 to 31707 ms.
  - `SshPrivateKeyReaderTests.Read_EncryptedOpenSshKeyWithoutOrWithAWrongPassphrase_ReadsNone`, both rows: `PHASE read without a passphrase` 0 ms, `PHASE read with a wrong passphrase` 19860 and 33060 ms.
  - `SshPrivateKeyReaderTests.Read_EcdsaOpenSshEncryptedWithAes256Gcm_ReadsTheKeyOfItsPublicKeyFile`: `PHASE read and decrypt` 35302 ms.
  - `SshPrivateKeyReaderTests.Read_RsaOpenSshEncrypted_ReadsAnRsaKey`: `PHASE read with the passphrase` 36795 ms, `PHASE read with a wrong passphrase` 46760 ms (83556 ms in all).
  - `OpenSshPrivateKeyDecoderTests.Read_WrongPassphrase_ThrowsAsTheCheckIntegersDiffer`, all three rows: `PHASE bcrypt-pbkdf and decrypt` 12476 to 29465 ms.
  - `OpenSshPrivateKeyDecoderTests.Read_GcmWrongPassphrase_ThrowsAsTheTagFails`: `PHASE bcrypt-pbkdf and decrypt` 20502 ms.
  - `OpenSshPrivateKeyDecoderTests.Read_GcmWithoutItsTag_Throws` (one round) printed `SLOW:` at 3676 ms on the first run and not on the second (`PHASE bcrypt-pbkdf and decrypt` 1682 ms).
  The cause is the hand-built bcrypt hash in `Curl.Cryptography.UnitLibrary`, already filed as BL-1558 (Backlog); no new task.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Keys test in Curl.Protocol.Ssh.UnitTests writes ARRANGE, ACT and ASSERT lines; 176 filtered and 1673 project tests pass
