---
id: BL-1085
title: Decrypt ENCRYPTED PRIVATE KEY --key files for Ed25519, Ed448 and ML-DSA client certificates
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1066]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1085 — Decrypt ENCRYPTED PRIVATE KEY --key files for Ed25519, Ed448 and ML-DSA client certificates

## Goal

`--cert` with an Ed25519, Ed448 or ML-DSA certificate and an `ENCRYPTED PRIVATE KEY` PEM `--key` decrypted with `--pass` presents the certificate in the hand-built TLS client, as OpenSSL-built curl does.

## Context

- BL-1066 / ADR-0301: `ClientCertificateLoader.LoadWithPrivateKey` reads these keys by hand with `HandBuiltPrivateKeyReader`, from the first unencrypted `PRIVATE KEY` PEM block only; an `ENCRYPTED PRIVATE KEY` gives exit 43 (`unable to set private key file`).
- The BCL's `Pkcs8PrivateKeyInfo` is in `System.Security.Cryptography.Pkcs`, not the shared framework. `Curl.Protocol.Ssh.UnitLibrary/Keys/Pkcs8Decryption.cs` already decrypts PBES2 PKCS #8, but Networking cannot reference a protocol library: hand-write (or move to a shared library under its own task) the PBES2 (PBKDF2 + AES-CBC) decryption.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` load an Ed25519 and an ML-DSA-44 certificate with a PBES2-encrypted PKCS #8 PEM key and `--pass`, and `HandBuiltTlsProvider.ToTlsClientCertificate` gives a signing key that `CanSign` its scheme.
- [x] A wrong passphrase gives exit 43 with `unable to set private key file: '<key>' type PEM`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: hand-written `EncryptedPrivateKeyInfoDecryption` (PBES2, PBKDF2 over HMAC-SHA-1/256/384/512, AES-128/192/256-CBC or DES-EDE3-CBC) in Networking; `ClientCertificateLoader.PemPrivateKeyInfo` takes the first `ENCRYPTED PRIVATE KEY` block when `--pass` is given. Decisions in ADR-0354: PBES1 and single DES refused (OpenSSL 3 legacy provider only); no passphrase skips encrypted blocks (Curl does not prompt), giving exit 43.
- The exit 43 message is the one ADR-0301 already pinned from measured OpenSSL-built curl; no OpenSSL build of curl on this Windows lane to re-measure.
- Tests: `EncryptedPrivateKeyInfoDecryptionTests` (a BCL-encrypted key round trip as an independent check, each cipher and PRF form, malformed structures) and three new `HandBuiltPrivateKeyReaderTests` cases. Measure-CodeQuality: Networking 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. An ENCRYPTED PRIVATE KEY --key with --pass loads for Ed25519, Ed448 and ML-DSA client certificates
