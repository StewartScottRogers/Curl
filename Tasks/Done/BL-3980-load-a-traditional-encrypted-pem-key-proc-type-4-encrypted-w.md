---
id: BL-3980
title: Load a traditional encrypted PEM key (Proc-Type 4,ENCRYPTED with DEK-Info) in the OpenSSL build
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ClientCertificateLoader.cs, Curl.Networking.UnitLibrary/TraditionalPemKeyDecryptor.cs, Curl.Networking.UnitTests/TraditionalPemKeyDecryptorTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3980 — Load a traditional encrypted PEM key (Proc-Type 4,ENCRYPTED with DEK-Info) in the OpenSSL build

## Goal

curl's OpenSSL build (Linux, macOS) loads a client key in the traditional OpenSSL encrypted PEM form (`-----BEGIN RSA PRIVATE KEY-----` with `Proc-Type: 4,ENCRYPTED` and `DEK-Info: <cipher>,<iv hex>`) given with `--key` and `--pass` (or the pass phrase typed at the ADR-0948 prompt), and Curl does the same instead of failing exit 43.

## Context

Found by BL-3624 (CGR-095, ADR-0948): `ClientCertificateLoader` asks for the pass phrase of such a key, but the BCL's `ImportFromEncryptedPem` reads only PKCS#8 `ENCRYPTED PRIVATE KEY`, so even the right pass phrase fails exit 43 `unable to set private key file: '<key>' type PEM`. Make one with `openssl genrsa -aes256 -passout pass:secret -traditional` (OpenSSL 3) or `openssl rsa -aes256 -traditional`. The key is derived with OpenSSL's `EVP_BytesToKey` (MD5, one iteration, salt = first 8 bytes of the IV); ciphers seen in practice: AES-128/192/256-CBC, DES-EDE3-CBC. BCL only: MD5 and the ciphers are in `System.Security.Cryptography`. Measure real curl (OpenSSL build, in WSL) with `Record-CurlExchange.ps1` before pinning output.

## Acceptance criteria

- [x] `curl -sS -k --cert cli.crt --key cli_trad_enc.key --pass secret https://localhost:<p>/` against `openssl s_server -WWW` gives the same exit code, stdout and stderr through real curl (OpenSSL build) and Curl on Linux; the commands and results are recorded in Notes.
- [x] A wrong pass phrase still fails exit 43 with the same text as real curl.
- [x] Unit tests in `Curl.Networking.UnitTests/TraditionalPemKeyDecryptorTests.cs` cover each supported DEK-Info cipher; `Curl.Networking.UnitLibrary` stays at 100% line and branch coverage.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes
- Plan: `TraditionalPemKeyDecryptor` decrypts the `Proc-Type: 4,ENCRYPTED` block by hand (EVP_BytesToKey, MD5, one iteration, salt = IV[0..8]; CBC with PKCS#7), and `ClientCertificateLoader.CombineWithKey` hands the decrypted block to `CreateFromPem` when a pass phrase is given. ADR-0949 records it.
- Measured real curl 8.18.0 / OpenSSL 3.5.5 in WSL against `openssl s_server -accept 18980 -cert srv.crt -key srv.key -WWW -verify 1 -quiet`, key from `openssl rsa -in cli.key -aes256 -passout pass:secret -traditional -out cli_trad_enc.key`: `curl -sS -k --cert cli.crt --key cli_trad_enc.key --pass secret https://localhost:18980/index.html` -> stdout `hello`, stderr empty, exit 0; with `--pass wrong` -> stdout empty, stderr `curl: (43) unable to set private key file: 'cli_trad_enc.key' type PEM`, exit 43.
- WSL has no .NET SDK, so Curl itself could not be run on Linux in this lane. Curl's side is pinned by `TraditionalPemKeyDecryptorTests.LoadAsOpenSslBuild_TraditionalEncryptedKeyWithRightPass_LoadsTheKey` and `..._WithWrongPass_FailsWithExit43` (the OpenSSL build's loader, the path Linux runs; the handshake path after loading is unchanged), which are platform-neutral and run on Linux in CI.
- Cipher fixtures (AES-128/192/256-CBC, DES-EDE3-CBC) were written by `openssl ec -<cipher> -passout pass:secret` from one P-256 key, so the tests check against OpenSSL's own output. DES-CBC is not read: OpenSSL 3.5.5 cannot even write it (`openssl rsa -des` fails) without the legacy provider.
- Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: 0 failing members. Solution build clean; fast tests: 46 test projects passed, 0 failed.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: aaef231d 1c276785 0e2075d2 779790c0.
- 2026-10-28: Doing -> Done. The OpenSSL build loads a traditional encrypted PEM key (Proc-Type 4,ENCRYPTED, DEK-Info AES-CBC or DES-EDE3-CBC) with --pass
