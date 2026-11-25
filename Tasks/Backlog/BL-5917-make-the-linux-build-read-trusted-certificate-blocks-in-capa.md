---
id: BL-5917
title: Make the Linux build read TRUSTED CERTIFICATE blocks in --capath files as OpenSSL does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-5879]
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.TrustedCertificate.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5917 — Make the Linux build read TRUSTED CERTIFICATE blocks in --capath files as OpenSSL does

## Goal

The OpenSSL build trusts, or rejects with exit 60 `certificate rejected (28)`, a certificate held as a `TRUSTED CERTIFICATE` block in a `--capath` hashed file, as OpenSSL curl does.

## Context

Follow-up from BL-5879 (ADR-1347), which honours these settings in `--cacert` only. `ImportHashedCertificates` in `ServerCertificateVerification.CaCertificates.cs` imports `CERTIFICATE` blocks alone, so a `<hash>.0` file in the trusted format adds nothing; OpenSSL's `by_dir` reads it with `PEM_read_bio_X509_AUX`. `OpenSslCaFile` already parses the format. Measure real curl (WSL, curl 8.18 OpenSSL) before pinning behaviour, a rejected `--capath` certificate beside a `--cacert` file included.

## Acceptance criteria

- [ ] A `--capath` hashed file holding a root trusted for `serverAuth` as a `TRUSTED CERTIFICATE` block verifies a server it signed (exit 0).
- [ ] The same file rejecting `serverAuth` gives exit 60 `certificate rejected (28)`.
- [ ] Focused unit tests in `Curl.Networking.UnitTests` pin both, and `dotnet build` and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
