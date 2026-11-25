---
id: BL-5927
title: Make the Linux build read X509 CERTIFICATE blocks in --cacert and --capath files as OpenSSL does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5917]
touches: [Curl.Networking.UnitLibrary/OpenSslCaFile.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.TrustedCertificate.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5927 — Make the Linux build read X509 CERTIFICATE blocks in --cacert and --capath files as OpenSSL does

## Goal

The OpenSSL build trusts a certificate held as an X509 CERTIFICATE block in a --capath hashed file, and in a --cacert file if real curl does.

## Context

Found by BL-5917 (ADR-1359). WSL curl 8.18.0 (OpenSSL 3.5.5) with --capath holding the root as an "X509 CERTIFICATE" block exits 0; OpenSslCaFile skips that label. Measure --cacert before pinning it.

## Acceptance criteria

- [ ] A --capath hashed file holding the root as an X509 CERTIFICATE block verifies a server it signed.
- [ ] --cacert with such a block behaves as measured curl does.
- [ ] Focused unit tests in Curl.Networking.UnitTests pin both, and dotnet build and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
