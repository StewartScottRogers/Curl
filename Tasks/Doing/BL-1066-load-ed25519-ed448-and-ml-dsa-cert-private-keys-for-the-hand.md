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
completed:
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

- [ ] `Curl.Networking.UnitTests` load a PEM certificate and PKCS #8 key for each of Ed25519, Ed448, ML-DSA-44, ML-DSA-65 and ML-DSA-87 and get a `TlsClientCertificate` whose `SigningKey.CanSign` accepts that key's scheme.
- [ ] An ML-DSA key in each of the `seed`, `expandedKey` and `both` forms loads; a malformed one gives the same exit code as an unreadable `--key` today (exit 58).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
