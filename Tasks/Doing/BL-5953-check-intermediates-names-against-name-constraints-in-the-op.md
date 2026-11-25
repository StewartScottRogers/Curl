---
id: BL-5953
title: Check intermediates' names against name constraints in the OpenSSL build, and report the first refusal when the chain engine flags several
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5950]
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs, Curl.Networking.UnitTests/NameConstraintVerifyResultTests.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5953 — Check intermediates' names against name constraints in the OpenSSL build, and report the first refusal when the chain engine flags several

## Goal

In the OpenSSL build, an intermediate whose own name breaks a higher CA's constraint, and a chain that two CAs refuse for different reasons below a self-signed `--cacert` root, report the verify result OpenSSL curl reports.

## Context

Found during BL-5950 (ADR-1391). Two gaps remain:
- `CertificateNameConstraints.FindViolation` checks only the leaf's names. OpenSSL's `check_name_constraints` (`crypto/x509/x509_vfy.c`) checks every certificate that is not self-issued (the leaf always) against the CAs above it, from the top certificate down, the CN check for the leaf only. Measured with curl 8.18.0 and OpenSSL 3.5.5 (WSL): root excluding email `bad.org`, intermediate permitting DNS `other.org` and holding subject emailAddress `ca@bad.org`, leaf `DNS:localhost`: 48 with the root as `--cacert`, 47 with the intermediate.
- `OpenSslVerifyResult.StatusOf` consults `FindViolation` only for a partial chain. With a self-signed root as `--cacert` the platform's chain engine reports the flags itself, and `OfExtensionsOrSignature` answers 47 whenever both 47 and 48 are flagged; OpenSSL answered 48 for a root excluding DNS `localhost` above an intermediate permitting DNS `other.org`. Check what .NET's Linux chain engine puts in each element's `ChainElementStatus`.

## Acceptance criteria

- [ ] With `matchesSchannelBuild` false, an intermediate breaking the root's constraint above a leaf breaking the intermediate's gives the verify result measured from OpenSSL curl, pinned by a unit test.
- [ ] With the OpenSSL build and a self-signed root as `--cacert`, a chain flagged both 47 and 48 reports the verify result measured from OpenSSL curl, pinned by a unit test.
- [ ] The Schannel build's answers do not change, and `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: b2112e5a e3771298 403a4f6f.
