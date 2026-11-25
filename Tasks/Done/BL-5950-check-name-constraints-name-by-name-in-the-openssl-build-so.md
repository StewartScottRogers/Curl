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
completed: 2026-11-25
---
# BL-5950 — Check name constraints name by name in the OpenSSL build, so the verify result is the first refused name's

## Goal

In the OpenSSL build, a leaf breaking more than one name constraint reports the verify result OpenSSL curl reports.

## Context

Found during BL-5942 (ADR-1389). `CertificateNameConstraints.FindViolation` checks every name against the permitted subtrees and only then every name against the excluded ones; it reads the CAs from the leaf upwards. OpenSSL's `NAME_CONSTRAINTS_check` (`crypto/x509/v3_ncons.c`) takes the names one at a time (permitted, then excluded, for each), and `check_name_constraints` (`crypto/x509/x509_vfy.c`) takes the constraining CAs from the top of the chain down. So a subject directoryName that is excluded beside an emailAddress outside the permitted domain is 48 in OpenSSL and 47 here. Read from the source, not measured: measure OpenSSL curl first. The Schannel build's answers must not change.

## Acceptance criteria

- [x] With `matchesSchannelBuild` false, a leaf whose earlier name is excluded and whose later name is not permitted gives the verify result measured from OpenSSL curl, pinned by a unit test.
- [x] With `matchesSchannelBuild` false, two constraining CAs that refuse the leaf for different reasons give the verify result measured from OpenSSL curl, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 / OpenSSL 3.5.5 (WSL Ubuntu, `openssl s_server`); table and decision in ADR-1390. Excluded subject directoryName beside an email not permitted is 48; the upper CA's refusal wins.
- OpenSSL also takes the alternative names in the extension's order (not DNS first), so the OpenSSL path now reads them in that order. Schannel's name list and CA order are unchanged, pinned by `matchesSchannelBuild` true data rows.
- Checks: `dotnet build` 0 warnings, fast tests exit 0, `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 0 failing members. Linux and macOS: tests are platform-neutral (certificates made in the test, verify result tests excluded on macOS like their neighbours).
- Filed BL-5951: intermediates' own names are not checked, and below a self-signed `--cacert` root the platform chain engine's flags answer 47 whenever both 47 and 48 are set.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 06797fe2 280569d4 3d836d5e.
- 2026-11-25: Doing -> Done. The OpenSSL build checks name constraints name by name from the top CA down, so the verify result is the first refused name's
