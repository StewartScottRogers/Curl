---
id: BL-608
title: Check --pinnedpubkey against the server's public key, failing with exit 90
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-607]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-608 — Check --pinnedpubkey against the server's public key, failing with exit 90

## Goal

After the TLS handshake, the server certificate's SubjectPublicKeyInfo is compared with `--pinnedpubkey` (a PEM or DER public-key file, or `sha256//<base64>` hashes separated by `;`), and a mismatch fails with exit 90 (`CURLE_SSL_PINNEDPUBKEYNOTMATCH`) and curl 8.21.0's message, on each platform as its build does.

## Context

- Conformance audit 2026-09-28, row 17 (Major). Option: BL-607.
- Code: `Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs`, `TlsFailureMessages.cs`. `X509Certificate2.PublicKey.ExportSubjectPublicKeyInfo()` and `SHA256` are in the BCL.
- Whether the Schannel build supports file pins and hash pins, and whether `-k` skips the check, must be measured.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Tls -k`: the right `sha256//` hash (compute it from the script's certificate), a wrong hash, a list with the right one second, a PEM key file, a missing file; stderr and exit code copied into Notes, per platform where they differ.
- [x] `Curl.Networking.UnitTests` pin each measured case with a generated test certificate, per platform with `OSCondition` where they differ.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- `Record-CurlExchange.ps1` could not give the served key before curl ran, so it gained
  `-TlsPublicKeyFile <path>` (writes the key as PEM, and as DER at `<path>.der`) and, with `-Tls`,
  replaces `{TlsPublicKeySha256}` in CurlArgs with the key's base64 SHA-256. Measured
  2026-09-29, `-sS -k`, curl 8.21.0 Schannel (Windows) and curl 8.18.0 OpenSSL 3.5.5 (WSL Ubuntu,
  `-Curl wsl.exe -ListenAddress <host>`):
  - right `sha256//` hash: exit 0, empty stderr, both builds.
  - wrong hash: exit 90, `curl: (90) SSL: public key does not match pinned public key`, both builds.
  - `sha256//<wrong>;sha256//<right>`: exit 0, both builds.
  - the key as a PEM file, and as a DER file: exit 0, both builds.
  - another key's PEM file, a file holding `not a key`, a missing file: exit 90, same text, both builds.
  - `sha256//` and `sha256//!!!` (Schannel): exit 90, same text.
  - right hash without `-k` (self-signed): exit 60 `SEC_E_UNTRUSTED_ROOT`, so verification is judged first.
  - Builds do not differ in exit code or `-sS` stderr, so no `OSCondition` split was needed; the tests
    pin both builds on every platform through the providers' build flag.
  - `-v` differs: Schannel prints `*  public key hash: sha256//<b64>` (hash pins only) and, on a
    mismatch, `* SSL: public key does not match pinned public key` twice; OpenSSL prints it once,
    after the certificate details. Left to follow-up BL-877.
- Design (ADR-0193): the check lives in `ServerCertificateVerification.Judge`, shared by both TLS
  providers and the hand-built QUIC verifier, rather than after each provider's handshake; that
  kept both provider methods under the complexity limit and needs one implementation.
- `--proxy-pinnedpubkey` stays with BL-611.
- Quality: Curl.Networking.UnitLibrary 100% line and branch, 0 failing members (629); Curl.Console
  100% and 100%, 0 failing.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --pinnedpubkey checks sha256// hashes and PEM/DER key files against the server key in both TLS providers, exit 90 on a mismatch
