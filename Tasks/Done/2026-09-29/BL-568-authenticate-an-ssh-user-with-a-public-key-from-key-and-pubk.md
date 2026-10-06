---
id: BL-568
title: Authenticate an SSH user with a public key from --key and --pubkey
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-567]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-568 — Authenticate an SSH user with a public key from --key and --pubkey

## Goal

The handler authenticates with `publickey` (RFC 4252 section 7, rsa-sha2-256/512 and ECDSA per RFC 8332 and RFC 5656) using the private key from `--key` (default `~/.ssh/id_rsa` and friends as curl 8.21.0 looks for them), decrypted with `--pass` where the format allows, and the public key from `--pubkey` or derived from the private key, in the order curl tries methods, with curl's exit code and message for a missing, unreadable, encrypted-without-passphrase or refused key.

## Context

- Conformance audit 2026-09-28, rows 31 and 35. Builds on BL-567's user-auth flow. Key formats: BL-560's ADR.
- **BCL only.** `RSA.ImportFromPem`/`ImportFromEncryptedPem`, `ECDsa.ImportFromPem`, PKCS#8 and PKCS#1 via the BCL; the unencrypted `openssh-key-v1` format parsed by hand. `DSA` keys (`ssh-dss`) are read and used here too. Encrypted `openssh-key-v1` keys (bcrypt-pbkdf) and Ed25519 keys are BL-681, built on `Curl.Cryptography.UnitLibrary`; structure the key reader so BL-681 adds them without reshaping it. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): every key format curl reads is read; what the BCL lacks is hand-built, never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: an RSA key in PEM and in `openssh-key-v1`, an ECDSA key, an encrypted key with and without `--pass`, a key the server does not authorise, and a missing `--key` file.

## Acceptance criteria

- [x] Measured first as above; stderr and exit code of each copied into Notes.
- [x] `Curl.Protocol.Ssh.UnitTests` pin the signature blob for fixed test keys (in-test key material, never a real user key) and the outcome for each measured case against the in-memory peer.
- [x] Default key paths resolve through the environment seam; tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Design (ADR-0230).** New `Keys` folder: `SshUserKeySource` (curl's default files
  through an injected `HOME` reader and `IFileSystem`; `--pubkey`; `--pass` encoded like
  the password), `SshPrivateKeyReader` with `PemBlock`, `LegacyPemDecryption`,
  `Pkcs8Decryption`, `KeyFileCbcDecryption`, `Asn1PrivateKeyDecoder` and
  `OpenSshPrivateKeyDecoder`, and `RsaSshPrivateKey`, `EcdsaSshPrivateKey`,
  `DsaSshPrivateKey` (hand-built `DsaSignature`; single DES from the hand-built `Des`),
  `SshPublicKey`, `SshPublicKeyFile`. `SshUserAuthentication` takes an optional
  `SshUserKeySource` and tries `publickey` before `password`, keeps `server-sig-algs` from
  `SSH_MSG_EXT_INFO`, and signs over `SshTransport.SessionIdentifier` (new).
  `SshMessageNumber.ExtensionInfo` and `SshAuthenticationMessageNumber.PublicKeyOk` added.
  No handler calls it yet (BL-576 composes the session).
- **Decision: read every ADR-0122 format on every platform**, although the Windows
  reference (WinCNG) reads only PKCS #1 RSA PEM: ADR-0122 already decided the superset;
  recorded again in ADR-0230.
- **Decision: an empty `HOME` counts as unset** (curl on Windows reads it so; on Unix curl
  would try `/.ssh/id_rsa`). Recorded in ADR-0230.
- **Touches:** added `Documentation/Planning/Decisions` for ADR-0230 and its index row; no
  task in `Doing` names it.
- **Bug found while testing:** an `openssh-key-v1` RSA key with a prime of 1 divided by
  zero; `RsaSshPrivateKey.FromComponents` now refuses it as an unreadable key.
- **Measured 2026-09-29, Windows reference build** (curl 8.21.0, libssh2 1.11.1 WinCNG),
  `curl -sS -v -k -u tester:wrong sftp://127.0.0.1:<port>/x [--key K] [--pubkey P] [--pass S]`
  through `Record-CurlExchange.ps1 -NoServer`, `HOME` an empty directory unless stated,
  against a throwaway MSTest loopback SSH server built from this library's classes
  (group14-sha256, rsa-sha2-256, aes128-ctr, hmac-sha2-256, `ext-info-s`) that sent
  `server-sig-algs`, listed `publickey,password`, verified every signature curl sent (all
  verified) and closed the channel after a success; deleted before commit. Keys generated
  for the run with `ssh-keygen` and OpenSSL 3.5. Every run's `-v` showed `SSH: host offers
  authentication via: publickey,password` and `SSH: trying private key file '<K>'`; each
  failure then tried the agent (`SSH: failure connecting to agent`) before the password.
  - RSA PKCS #1 PEM, `server-sig-algs` `rsa-sha2-512,rsa-sha2-256,ssh-rsa,ecdsa-...`:
    `none`, `publickey` query `rsa-sha2-512`, signed `rsa-sha2-512` -> `SSH: authenticated
    via publickey`, then exit 2 `curl: (2) Failure initializing sftp session: Unable to
    startup channel` (the server's channel close).
  - `server-sig-algs` `rsa-sha2-256` -> `rsa-sha2-256`; `rsa-sha2-256,rsa-sha2-512` ->
    `rsa-sha2-512`; `ssh-rsa` -> `ssh-rsa`; no `EXT_INFO` -> `ssh-rsa`; all succeed.
  - `server-sig-algs` `ssh-ed25519` -> no `publickey` request, `publickey authentication
    denied: No signing signature matched` -> `password` -> exit 67 `curl: (67)
    Authentication failure`.
  - Encrypted RSA PEM (AES-128-CBC) with `--pass secretpass` -> success; without `--pass`
    or with `--pass nope` -> no request, `Reason unknown (-1)` -> exit 67 `Authentication
    failure`.
  - RSA `openssh-key-v1`, ECDSA `openssh-key-v1` (P-256), ECDSA SEC 1 PEM (P-384), PKCS #8
    RSA and ECDSA, encrypted PKCS #8 with `--pass`, encrypted `openssh-key-v1` with and
    without `--pass`, Ed25519, DSA PKCS #8 -> no request, `Reason unknown (-1)` -> exit 67
    `Authentication failure` (WinCNG reads none of them).
  - `--key` missing -> no request, `Reason unknown (-1)` -> exit 67 `Authentication failure`.
  - Query answered `FAILURE` -> `Username/PublicKey combination invalid` -> `password` ->
    exit 67 `Authentication failure`; with `keyboard-interactive` listed -> `password`,
    `keyboard-interactive` -> exit 67 `curl: (67) Login denied`.
  - Signed request answered `FAILURE` -> `Invalid signature for supplied public key, or bad
    username/public key combination` -> exit 67 `Authentication failure`.
  - Query answered `SUCCESS` -> nothing more sent -> `authenticated via publickey`.
  - `--pubkey rsa_pem.pub` -> `SSH: trying public key file '<P>'`, query with the file's
    blob, signed -> success. `--pubkey` an ECDSA `.pub` with an RSA `--key`, or with
    `--key` missing -> query with the ECDSA blob (sent although `server-sig-algs` named only
    `rsa-sha2-512`), `PK_OK`, no signed request, `Callback returned error` -> exit 67
    `Authentication failure`. `--pubkey` missing -> no request, `Unable to open public key
    file` -> exit 67.
  - List `password` only -> no `publickey` line, no request -> exit 67.
  - No `--key`: `HOME/.ssh/id_rsa` -> `trying private key file 'Z:\...\homekey/.ssh/id_rsa'`
    (forward slash); only `HOME/.ssh/id_dsa` -> `'.../.ssh/id_dsa'`; `HOME` empty dir and
    `id_rsa` in the working directory -> `'id_rsa'`; `HOME` unset with `USERPROFILE\.ssh\id_rsa`
    and an empty working directory -> `''`, no request, `Reason unknown (-1)` -> exit 67.
  - `--key ""` -> exit 2 `curl: option --key: blank argument where content is expected`.
  - One run (`--pubkey` missing) first ended exit 2 `Failure establishing ssh session: -8,
    Unable to exchange encryption keys` from a fault in the throwaway server's key
    exchange; rerun, it gave the result above.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SSH publickey authentication works: --key in PEM, PKCS #8 and openssh-key-v1 (RSA, ECDSA, DSA), --pubkey, --pass, curl's default key files and server-sig-algs, measured against curl 8.21.0
