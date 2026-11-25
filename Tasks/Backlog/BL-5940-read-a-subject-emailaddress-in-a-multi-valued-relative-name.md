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
completed:
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

- [ ] With `matchesSchannelBuild` false, a subject emailAddress in a multi-valued relative name inside an excluded email domain exits 60 with the verify result OpenSSL curl reports, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
