---
id: BL-5950
title: Check name constraints name by name in the OpenSSL build, so the verify result is the first refused name's
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5942]
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5950 — Check name constraints name by name in the OpenSSL build, so the verify result is the first refused name's

## Goal

In the OpenSSL build, a leaf breaking more than one name constraint reports the verify result OpenSSL curl reports.

## Context

Found during BL-5942 (ADR-1388). `CertificateNameConstraints.FindViolation` checks every name against the permitted subtrees and only then every name against the excluded ones; it reads the CAs from the leaf upwards. OpenSSL's `NAME_CONSTRAINTS_check` (`crypto/x509/v3_ncons.c`) takes the names one at a time (permitted, then excluded, for each), and `check_name_constraints` (`crypto/x509/x509_vfy.c`) takes the constraining CAs from the top of the chain down. So a subject directoryName that is excluded beside an emailAddress outside the permitted domain is 48 in OpenSSL and 47 here. Read from the source, not measured: measure OpenSSL curl first. The Schannel build's answers must not change.

## Acceptance criteria

- [ ] With `matchesSchannelBuild` false, a leaf whose earlier name is excluded and whose later name is not permitted gives the verify result measured from OpenSSL curl, pinned by a unit test.
- [ ] With `matchesSchannelBuild` false, two constraining CAs that refuse the leaf for different reasons give the verify result measured from OpenSSL curl, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
