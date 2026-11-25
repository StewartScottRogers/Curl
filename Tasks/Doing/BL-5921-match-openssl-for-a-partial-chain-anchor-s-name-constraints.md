---
id: BL-5921
title: Match OpenSSL for a partial-chain anchor's name constraints on a subject CN holding a NUL (53) or inside a multi-valued name
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5921 — Match OpenSSL for a partial-chain anchor's name constraints on a subject CN holding a NUL (53) or inside a multi-valued name

## Goal

In the OpenSSL build, a leaf with no dNSName under a partial-chain anchor's name constraints gets OpenSSL curl's verdict when its subject CN holds a NUL or sits in a multi-valued relative name.

## Context

Left over from BL-5829 (ADR-1354). `CertificateNameConstraints` refuses a CN with a NUL inside it as
`permitted subtree violation (47)`, where OpenSSL's `cn2dnsid` gives `X509_V_ERR_UNSUPPORTED_NAME_SYNTAX` (53);
and `ListCommonNames` skips a CN in a multi-valued relative name in both builds, where OpenSSL's
`NAME_CONSTRAINTS_check_CN` reads every CN attribute. The Schannel build's answers must not change
(BL-5432, BL-5497). Measure OpenSSL curl first.

## Acceptance criteria

- [ ] With `matchesSchannelBuild` false, a CN-only leaf whose CN holds an inner NUL under a constrained partial-chain anchor exits 60 with the verify result OpenSSL curl reports, pinned by a unit test.
- [ ] With `matchesSchannelBuild` false, a dotted host-name CN in a multi-valued relative name inside an excluded DNS subtree exits 60 with `(48)`, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: b5bb68c9 79a47cd0 3d836d5e.
