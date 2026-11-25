---
id: BL-5942
title: Answer a subject emailAddress that is not an IA5String under name constraints as the OpenSSL build
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5940]
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5942 — Answer a subject emailAddress that is not an IA5String under name constraints as the OpenSSL build

## Goal

In the OpenSSL build, a leaf whose subject emailAddress is not an IA5String under a CA with name constraints answers as OpenSSL curl does.

## Context

Found during BL-5940 (ADR-1387). OpenSSL's `NAME_CONSTRAINTS_check` (`crypto/x509/v3_ncons.c`) returns `X509_V_ERR_UNSUPPORTED_NAME_SYNTAX` (53) for a subject emailAddress that is not an IA5String; `CertificateNameConstraints` decodes any character string type and checks it as an rfc822Name. Read from the source, not yet measured: measure OpenSSL curl first (`openssl req` forces IA5String, so build the certificate another way). The Schannel build's answers must not change.

## Acceptance criteria

- [ ] With `matchesSchannelBuild` false, a UTF8String subject emailAddress under a name-constrained CA gives the exit code and verify result measured from OpenSSL curl, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: a1374463 3d836d5e.
