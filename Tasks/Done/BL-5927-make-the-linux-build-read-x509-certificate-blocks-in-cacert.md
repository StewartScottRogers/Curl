---
id: BL-5927
title: Make the Linux build read X509 CERTIFICATE blocks in --cacert and --capath files as OpenSSL does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5917]
touches: [Curl.Networking.UnitLibrary/OpenSslCaFile.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.TrustedCertificate.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5927 — Make the Linux build read X509 CERTIFICATE blocks in --cacert and --capath files as OpenSSL does

## Goal

The OpenSSL build trusts a certificate held as an X509 CERTIFICATE block in a --capath hashed file, and in a --cacert file if real curl does.

## Context

Found by BL-5917 (ADR-1363). WSL curl 8.18.0 (OpenSSL 3.5.5) with --capath holding the root as an "X509 CERTIFICATE" block exits 0; OpenSslCaFile skips that label. Measure --cacert before pinning it.

## Acceptance criteria

- [x] A --capath hashed file holding the root as an X509 CERTIFICATE block verifies a server it signed.
- [x] --cacert with such a block behaves as measured curl does.
- [x] Focused unit tests in Curl.Networking.UnitTests pin both, and dotnet build and the fast tests pass.

## Notes

- Measured with WSL curl 8.18.0 (OpenSSL 3.5.5): --cacert and --capath both accept an X509
  CERTIFICATE root (exit 0); an undecodable such block makes --cacert exit 77; trust settings after a
  CERTIFICATE or X509 CERTIFICATE block are ignored from --cacert and honoured from --capath
  (exit 60, certificate rejected (28)). Decision in ADR-1375; the settings difference is matched too,
  since it is one flag in the same reader.
- Added to touches ServerCertificateVerification.CaCertificates.cs (two comments that would have
  become untrue) and Curl.Networking.UnitLibrary/CLAUDE.md; no task in Doing named either.
- Quality run for Curl.Networking.UnitLibrary: 100% line and branch, 0 failing members.
- The tests judge chains with matchesSchannelBuild false and no platform path or OS text, so they
  are platform-neutral.
- An empty directory Z:\tmp-bl5927, made while measuring and unused, was left behind: its removal
  was refused by the permission guard.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 49d5ed85 a5658766 85214380.
- 2026-11-25: Doing -> Done. X509 CERTIFICATE blocks read from --cacert and --capath as measured curl does (ADR-1375)
