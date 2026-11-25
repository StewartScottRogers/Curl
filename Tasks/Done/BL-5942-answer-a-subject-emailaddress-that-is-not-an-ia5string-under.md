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
completed: 2026-11-25
---
# BL-5942 — Answer a subject emailAddress that is not an IA5String under name constraints as the OpenSSL build

## Goal

In the OpenSSL build, a leaf whose subject emailAddress is not an IA5String under a CA with name constraints answers as OpenSSL curl does.

## Context

Found during BL-5940 (ADR-1387). OpenSSL's `NAME_CONSTRAINTS_check` (`crypto/x509/v3_ncons.c`) returns `X509_V_ERR_UNSUPPORTED_NAME_SYNTAX` (53) for a subject emailAddress that is not an IA5String; `CertificateNameConstraints` decodes any character string type and checks it as an rfc822Name. Read from the source, not yet measured: measure OpenSSL curl first (`openssl req` forces IA5String, so build the certificate another way). The Schannel build's answers must not change.

## Acceptance criteria

- [x] With `matchesSchannelBuild` false, a UTF8String subject emailAddress under a name-constrained CA gives the exit code and verify result measured from OpenSSL curl, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 / OpenSSL 3.5.5 (WSL): any subject emailAddress that is not an IA5String under name constraints is exit 60, verify result 53; the subject directoryName and earlier IA5 emailAddresses are judged first; alternative names are not reached; no constraints, exit 0. Table and decision in ADR-1388. An OCTET STRING emailAddress makes a certificate OpenSSL cannot load at all.
- Build clean. `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members, its fast tests of the 22 test projects reaching the library all passed.
- Linux and macOS: the new tests build subjects with `AsnWriter`; the `VerifyPeer` one excludes macOS, as its siblings do.
- Filed BL-5950: name-by-name and CA order of the OpenSSL check (read from source).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: a1374463 3d836d5e.
- 2026-11-25: Doing -> Done. OpenSSL build answers 53 for a subject emailAddress that is not an IA5String under name constraints
