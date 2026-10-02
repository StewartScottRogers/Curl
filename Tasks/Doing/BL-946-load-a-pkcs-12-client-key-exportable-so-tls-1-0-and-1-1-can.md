---
id: BL-946
title: Load a PKCS #12 client key exportable so TLS 1.0 and 1.1 can sign with it on Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-804]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-946 — Load a PKCS #12 client key exportable so TLS 1.0 and 1.1 can sign with it on Windows

## Goal

On Windows, an RSA client certificate given as a PKCS #12 file with `--cert` signs a TLS 1.0 or 1.1 CertificateVerify through the hand-built client instead of sending an empty Certificate.

## Context

- BL-804 (ADR-0195) made `RsaTlsSigningKey` sign TLS 1.0/1.1's MD5 + SHA-1 block with the hand-built `RsaCrtPrivateKey`, which needs `RSA.ExportParameters(true)`. A key that refuses export does not fit that rule, so the client sends an empty Certificate.
- `ClientCertificateLoader` (`Curl.Networking.UnitLibrary`) loads PKCS #12 with `X509CertificateLoader.LoadPkcs12(contents, passphrase)` and no `X509KeyStorageFlags`. On Windows that gives an `RSACng` whose export policy forbids plaintext export; on Linux and macOS the key exports.
- Load with `X509KeyStorageFlags.Exportable` (keeping `EphemeralKeySet` semantics as today) in both the Schannel-build and OpenSSL-build PKCS #12 paths, and in the PEM path's round trip through PKCS #12 (`ClientCertificateLoader.cs` near line 375). A key from the Windows certificate store stays as the store gives it.

## Acceptance criteria

- [ ] A `ClientCertificateLoaderTests` test loads an RSA PKCS #12 file and shows `GetRSAPrivateKey()!.ExportParameters(true)` succeeds on every platform.
- [ ] A `HandBuiltTlsProviderTests` test shows `ToTlsClientCertificate` of that certificate gives a key that `CanSign` the legacy RSA rule.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Renumbered from BL-877, which the --pinnedpubkey -v task filed first also holds.
- 2026-10-02: Backlog -> Doing.
