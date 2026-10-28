---
id: BL-3966
title: Load a traditional encrypted PEM key (Proc-Type 4,ENCRYPTED with DEK-Info) in the OpenSSL build
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ClientCertificateLoader.cs, Curl.Networking.UnitLibrary/TraditionalPemKeyDecryptor.cs, Curl.Networking.UnitTests/TraditionalPemKeyDecryptorTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3966 — Load a traditional encrypted PEM key (Proc-Type 4,ENCRYPTED with DEK-Info) in the OpenSSL build

## Goal

curl's OpenSSL build (Linux, macOS) loads a client key in the traditional OpenSSL encrypted PEM form (`-----BEGIN RSA PRIVATE KEY-----` with `Proc-Type: 4,ENCRYPTED` and `DEK-Info: <cipher>,<iv hex>`) given with `--key` and `--pass` (or the pass phrase typed at the ADR-0948 prompt), and Curl does the same instead of failing exit 43.

## Context

Found by BL-3624 (CGR-095, ADR-0948): `ClientCertificateLoader` asks for the pass phrase of such a key, but the BCL's `ImportFromEncryptedPem` reads only PKCS#8 `ENCRYPTED PRIVATE KEY`, so even the right pass phrase fails exit 43 `unable to set private key file: '<key>' type PEM`. Make one with `openssl genrsa -aes256 -passout pass:secret -traditional` (OpenSSL 3) or `openssl rsa -aes256 -traditional`. The key is derived with OpenSSL's `EVP_BytesToKey` (MD5, one iteration, salt = first 8 bytes of the IV); ciphers seen in practice: AES-128/192/256-CBC, DES-EDE3-CBC. BCL only: MD5 and the ciphers are in `System.Security.Cryptography`. Measure real curl (OpenSSL build, in WSL) with `Record-CurlExchange.ps1` before pinning output.

## Acceptance criteria

- [ ] `curl -sS -k --cert cli.crt --key cli_trad_enc.key --pass secret https://localhost:<p>/` against `openssl s_server -WWW` gives the same exit code, stdout and stderr through real curl (OpenSSL build) and Curl on Linux; the commands and results are recorded in Notes.
- [ ] A wrong pass phrase still fails exit 43 with the same text as real curl.
- [ ] Unit tests in `Curl.Networking.UnitTests/TraditionalPemKeyDecryptorTests.cs` cover each supported DEK-Info cipher; `Curl.Networking.UnitLibrary` stays at 100% line and branch coverage.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
