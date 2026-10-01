---
id: BL-1066
title: Load Ed25519, Ed448 and ML-DSA --cert private keys for the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1064]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1066 — Load Ed25519, Ed448 and ML-DSA --cert private keys for the hand-built TLS client

## Goal

`--cert` with an Ed25519, Ed448 or ML-DSA-44/65/87 private key (PEM `PRIVATE KEY`, PKCS #8) presents the certificate and signs the CertificateVerify in the hand-built TLS client, as OpenSSL-built curl does.

## Context

- `HandBuiltTlsProvider.ToTlsClientCertificate` (`Curl.Networking.UnitLibrary`) maps only the RSA and ECDSA keys the BCL's `X509Certificate2` exposes; any other key gives no client certificate.
- `Curl.Tls.UnitLibrary` has the signing keys: `Ed25519TlsSigningKey(byte[] seed)`, `Ed448TlsSigningKey(byte[] seed)` (BL-1064) and `MlDsaTlsSigningKey(Curl.Cryptography.MlDsa)` (BL-1064).
- PKCS #8 OIDs: Ed25519 `1.3.101.112`, Ed448 `1.3.101.113` (the private key is an OCTET STRING wrapping the seed, RFC 8410), ML-DSA-44/65/87 `2.16.840.1.101.3.4.3.17/.18/.19` (RFC 9881's `ML-DSA-PrivateKey` CHOICE: `seed` [0], `expandedKey` OCTET STRING, or `both`; `MlDsa.GenerateKey(set, seed)` and `MlDsa.ImportPrivateKey` cover the two forms).
- Read the PKCS #8 with `System.Formats.Asn1`; find where `--cert`/`--key` PEM is loaded and keep the raw key bytes when the BCL cannot.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` load a PEM certificate and PKCS #8 key for each of Ed25519, Ed448, ML-DSA-44, ML-DSA-65 and ML-DSA-87 and get a `TlsClientCertificate` whose `SigningKey.CanSign` accepts that key's scheme.
- [x] An ML-DSA key in each of the `seed`, `expandedKey` and `both` forms loads; a malformed one gives the same exit code as an unreadable `--key` today (exit 58).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: `HandBuiltPrivateKeyReader` (new, `Curl.Networking.UnitLibrary`) reads the PKCS #8 with `System.Formats.Asn1` into `Ed25519TlsSigningKey`, `Ed448TlsSigningKey` or `MlDsaTlsSigningKey`; `ClientCertificateLoader.LoadWithPrivateKey` sends a certificate whose public key is one of those five algorithms there instead of the BCL, and returns a `HandBuiltKeyCertificate` (an `X509Certificate2` copy carrying the `SigningKey`), which `HandBuiltTlsProvider.ToTlsClientCertificate` takes first. ADR-0301.
- Decided: route by the certificate's key algorithm rather than "BCL first, then hand-built", because on Linux with OpenSSL 3.5+ the BCL would accept an ML-DSA key the hand-built client then could not sign with, and elsewhere it throws `PlatformNotSupportedException`; one path behaves the same on every platform.
- Decided: a malformed key, one of another algorithm, or one that is not the certificate's gives exit 43 (`unable to set private key file`), because that is what an unusable `--key` gives today (measured 2026-09-26, pinned in `SslStreamTlsProviderTests`); the criterion's "(exit 58)" was a slip, and its "same exit code as ... today" wins.
- Follow-up filed: BL-1085, `ENCRYPTED PRIVATE KEY` for these algorithms (the BCL's PKCS #8 decryption is in a package; SSH's `Pkcs8Decryption` cannot be referenced from Networking).
- Tests: `HandBuiltPrivateKeyReaderTests`, 34 cases (PEM and DER, all five algorithms, the three ML-DSA forms, 16 malformed or unreadable keys). `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. An Ed25519, Ed448 or ML-DSA --cert with its PKCS #8 key is presented and signs in the hand-built TLS client
