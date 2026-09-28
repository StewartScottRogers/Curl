---
id: BL-568
title: Authenticate an SSH user with a public key from --key and --pubkey
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-567]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-568 — Authenticate an SSH user with a public key from --key and --pubkey

## Goal

The handler authenticates with `publickey` (RFC 4252 section 7, rsa-sha2-256/512 and ECDSA per RFC 8332 and RFC 5656) using the private key from `--key` (default `~/.ssh/id_rsa` and friends as curl 8.21.0 looks for them), decrypted with `--pass` where the format allows, and the public key from `--pubkey` or derived from the private key, in the order curl tries methods, with curl's exit code and message for a missing, unreadable, encrypted-without-passphrase or refused key.

## Context

- Conformance audit 2026-09-28, rows 31 and 35. Builds on BL-567's user-auth flow. Key formats: BL-560's ADR.
- **BCL only.** `RSA.ImportFromPem`/`ImportFromEncryptedPem`, `ECDsa.ImportFromPem`, PKCS#8 and PKCS#1 via the BCL; the unencrypted `openssh-key-v1` format parsed by hand. Encrypted `openssh-key-v1` keys need bcrypt-pbkdf and Ed25519 keys need Ed25519 signing, neither in the BCL: do what BL-560's ADR decided. If a key format the ADR requires cannot be built on the BCL, move the task to `Blocked` for Stewart naming what is missing; never add a package.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: an RSA key in PEM and in `openssh-key-v1`, an ECDSA key, an encrypted key with and without `--pass`, a key the server does not authorise, and a missing `--key` file.

## Acceptance criteria

- [ ] Measured first as above; stderr and exit code of each copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the signature blob for fixed test keys (in-test key material, never a real user key) and the outcome for each measured case against the in-memory peer.
- [ ] Default key paths resolve through the environment seam; tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
