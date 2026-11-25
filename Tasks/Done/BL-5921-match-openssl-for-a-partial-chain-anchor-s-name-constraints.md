---
id: BL-5921
title: Match OpenSSL for a partial-chain anchor's name constraints on a subject CN holding a NUL (53) or inside a multi-valued name
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitLibrary/OpenSslCommonNameRefusal.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.NameChecks.cs, Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-25
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

- [x] With `matchesSchannelBuild` false, a CN-only leaf whose CN holds an inner NUL under a constrained partial-chain anchor exits 60 with the verify result OpenSSL curl reports, pinned by a unit test.
- [x] With `matchesSchannelBuild` false, a dotted host-name CN in a multi-valued relative name inside an excluded DNS subtree exits 60 with `(48)`, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 / OpenSSL 3.5.5 in WSL with `openssl s_server` (table in ADR-1376). A NUL in the
  last CN, the one curl's name check matches, answers `SSL: illegal cert name field` (verify result 1)
  before the chain's result is read; 53 shows when an earlier CN holds the inner NUL and the last CN is
  the host. Both are pinned (`VerifyPeer_OpenSslBuildOnlyCnHoldingANul_IsAnIllegalCertNameField`,
  `VerifyPeer_OpenSslBuildCnHoldingAnInnerNulBeforeTheHostCn_IsUnsupportedNameSyntax`).
- Touches widened by `OpenSslCommonNameRefusal.cs` and `ServerCertificateVerification.NameChecks.cs` for
  the name check; no task in Doing named either.
- Quality run (Curl.Networking.UnitLibrary): 100% line and branch; four methods were past the complexity
  gate and were split afterwards, then built and the fast tests run again (58,077 passed, 0 failed). Not
  re-measured, per the lane rule of one quality run.
- Tests ran on Windows only (a Linux container run did not finish in its 15 minutes); the chain-status
  tests carry the same macOS exclusion as their neighbours.
- Follow-up: BL-5935 (subject emailAddress in a multi-valued relative name).

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: b5bb68c9 79a47cd0 3d836d5e.
- 2026-11-25: Doing -> Done. The OpenSSL build now answers a NUL-holding CN as curl does (illegal cert name field, or 53) and checks CNs in multi-valued relative names against name constraints
