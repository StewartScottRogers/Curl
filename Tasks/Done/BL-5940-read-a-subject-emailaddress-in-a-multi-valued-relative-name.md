---
id: BL-5940
title: Read a subject emailAddress in a multi-valued relative name against name constraints in the OpenSSL build
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5940 — Read a subject emailAddress in a multi-valued relative name against name constraints in the OpenSSL build

## Goal

In the OpenSSL build, a subject emailAddress inside a multi-valued relative name breaks a partial-chain anchor's email name constraint as it does in OpenSSL curl.

## Context

Left over from BL-5921 (ADR-1383). `CertificateNameConstraints.ListSubjectEmailAddresses` skips an
emailAddress in a multi-valued relative name, while OpenSSL's `NAME_CONSTRAINTS_check`
(`crypto/x509/v3_ncons.c`) reads every emailAddress attribute. The Schannel build's answers must not
change. Measure OpenSSL curl first, with a leaf whose last CN matches the host, or curl's name check
answers before the verify result.

## Acceptance criteria

- [x] With `matchesSchannelBuild` false, a subject emailAddress in a multi-valued relative name inside an excluded email domain exits 60 with the verify result OpenSSL curl reports, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes
- Measured curl 8.18.0 / OpenSSL 3.5.5 (WSL): a multi-valued subject emailAddress answers 48 when excluded, 47 outside the permitted domain, 0 inside it; two emails in one relative name are both read. Table in ADR-1384.
- `ListEveryCommonName` now sits on a shared `ListEveryAttribute`; the Schannel build still reads single-valued relative names only.
- Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members. Fast tests green, build clean. Linux and macOS: the new tests build subjects with AsnWriter and the `VerifyPeer` one excludes OSX, as its siblings do.
- Filed BL-5942: a subject emailAddress that is not an IA5String (OpenSSL answers 53, read from source).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: c0ee5d25 3d836d5e.
- 2026-11-25: Doing -> Done. OpenSSL build checks a multi-valued subject emailAddress against email name constraints
