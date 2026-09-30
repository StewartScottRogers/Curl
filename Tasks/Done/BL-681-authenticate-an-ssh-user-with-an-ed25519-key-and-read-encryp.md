---
id: BL-681
title: Authenticate an SSH user with an Ed25519 key and read encrypted openssh-key-v1 keys
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-568, BL-672, BL-674, BL-668, BL-737]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-681 — Authenticate an SSH user with an Ed25519 key and read encrypted openssh-key-v1 keys

## Goal

`--key` accepts an Ed25519 private key (`openssh-key-v1`, unencrypted or encrypted) and an encrypted `openssh-key-v1` RSA or ECDSA key decrypted with `--pass` through bcrypt-pbkdf, and the handler authenticates with `ssh-ed25519` public-key signatures, with curl 8.21.0's exit code and message for a wrong passphrase.

## Context

- Conformance audit 2026-09-28, rows 31 and 35; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-568 (public-key user auth and the unencrypted `openssh-key-v1` parser). Primitives: Ed25519 BL-672, Blowfish/bcrypt-pbkdf BL-674; the key's cipher (`aes256-ctr` by default, `aes256-gcm@openssh.com` possible) comes from the BCL.
- OpenSSH `PROTOCOL.key`: `kdfname` `bcrypt`, `kdfoptions` (salt, rounds), the check integers that detect a wrong passphrase.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: an Ed25519 key, an encrypted Ed25519 key with the right and a wrong `--pass`, an encrypted RSA key; stderr and exit code.
- Reference rule: BL-668 (add the `Curl.Cryptography.UnitLibrary` reference and amend `Curl.Protocol.Ssh.UnitLibrary/CLAUDE.md` if no earlier SSH task did).

## Acceptance criteria

- [x] Measured first as above; stderr and exit code of each copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` decrypt in-test encrypted keys (generated once with `ssh-keygen -a 16`, committed as test data, never a real user key), pin the Ed25519 signature blob for a fixed session identifier, and pin each measured outcome against the in-memory peer.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Design (ADR-0263).** New `Keys/Ed25519SshPrivateKey` (signs `ssh-ed25519` with the
  hand-built `Ed25519`; built from `openssh-key-v1`'s two fields, which must agree, or from a
  PKCS #8 seed) and `Keys/OpenSshPrivateSectionDecryption` (bcrypt-pbkdf key and IV, then
  `aes{128,192,256}-ctr` through the hand-built `AesCtr`, `aes*-cbc` and `3des-cbc` through
  the BCL without padding, `aes{128,256}-gcm@openssh.com` through the BCL's `AesGcm` with the
  tag after the section). `OpenSshPrivateKeyDecoder.Read` now takes the passphrase;
  `Asn1PrivateKeyDecoder.ReadPkcs8` reads OID 1.3.101.112. `SshUserAuthentication` needed no
  change: a key that does not open is already the measured unreadable-key path.
- **Decision: libssh2's cipher set.** `chacha20-poly1305@openssh.com` keys are not read,
  although the primitives exist, because libssh2 1.11.1 refuses them (measured). ADR-0263.
- **Decision: every platform reads them**, the Windows build included, as ADR-0122 decided.
- **Touches:** added `Documentation/Planning/Decisions` for ADR-0263 and its index row; no
  task in `Doing` names it.
- **Measurement.** No 8.21.0 OpenSSL build is on the machine, and the Windows reference
  (WinCNG) reads neither Ed25519 nor encrypted `openssh-key-v1` (BL-568's measurement,
  2026-09-29: `SSH: publickey authentication denied: Reason unknown (-1)`, exit 67
  `curl: (67) Authentication failure`). So the reading side was measured with Ubuntu's curl
  8.18.0 (libssh2 1.11.1, OpenSSL 3.5.5) in WSL against OpenSSH 10.2p1's `sshd` unpacked with
  `apt-get download` (plus `libwrap0`) and run unprivileged on 127.0.0.1:2281
  (`PasswordAuthentication yes`, `KbdInteractiveAuthentication no`, `PerSourcePenalties no`,
  the keys in `AuthorizedKeysFile`), `curl -sS -v -k -u <user>:wrong --key K [--pass P]
  [--pubkey P] sftp://127.0.0.1:2281/tmp/bl681/hello.txt`, `HOME` an empty directory.
  `Record-CurlExchange.ps1` runs the Windows curl only, so it was not the tool for the Linux
  build. Every run showed `SSH authentication methods available: publickey,password` and
  `Using SSH private key file '<K>'` (curl 8.18's wording; 8.21.0's is ADR-0262's).
  - Ed25519 unencrypted -> `Initialized SSH public key authentication`, `Authentication
    complete`, stdout `hello`, exit 0.
  - Ed25519 `aes256-ctr`, `--pass secret` -> exit 0. Ed25519 `aes256-gcm@openssh.com`,
    `--pass secret` -> exit 0. ECDSA P-256 `aes256-gcm@openssh.com`, `--pass secret` -> exit 0.
  - Ed25519 `aes256-ctr` with `--pass nope`, with no `--pass`, and `aes256-gcm` with `--pass
    nope` -> `SSH public key authentication failed: Unable to extract public key from private
    key file: Wrong passphrase or invalid/unrecognized private key file format`, `Failure
    connecting to agent`, `Authentication failure`, stderr `curl: (67) Authentication
    failure`, exit 67.
  - Ed25519 `chacha20-poly1305@openssh.com`, `--pass secret` -> the same as a wrong
    passphrase, exit 67.
  - `aes256-ctr`, `--pass nope`, `--pubkey` the right `.pub` -> `SSH public key authentication
    failed: Callback returned error`, exit 67 `Authentication failure`.
  - With `KbdInteractiveAuthentication` left on, each failure was exit 67 `curl: (67) Login
    denied`, as BL-568 measured.
- **Follow-up filed:** BL-989 — the unreadable key's `-v` reason off Windows is the OpenSSL
  build's `Unable to extract public key ...`, not WinCNG's `Reason unknown (-1)`.
- **Test data:** the existing Ed25519 test key re-encrypted by `ssh-keygen -p -a 16 -Z
  <cipher>` under all ten OpenSSH ciphers, and the P-256 test key under
  `aes256-gcm@openssh.com`, passphrase `secret` (`TestUserKeys`). The Ed25519 signature blob
  over `ConnectAsync`'s session identifier is pinned in
  `SshUserAuthenticationTests.PublicKey.cs`; stable over three runs.
- **Results:** `Curl.Protocol.Ssh.UnitTests` 1136 passed; solution fast tests all green;
  `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` 100% line, 100% branch,
  645 members, 0 failing (`OpenSshPrivateKeyDecoder.Read` split to stay under complexity 10).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --key reads Ed25519 keys and bcrypt-encrypted openssh-key-v1 keys (AES CTR/CBC/GCM, 3DES) with --pass, and publickey signs ssh-ed25519; a wrong passphrase is exit 67 as measured
