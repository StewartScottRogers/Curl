---
id: BL-6059
title: Answer 51, not 53, for an otherName constraint under a root anchor in the OpenSSL build
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs, Curl.Networking.UnitTests/NameConstraintVerifyResultTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6059 — Answer 51, not 53, for an otherName constraint under a root anchor in the OpenSSL build

## Goal

With a self-signed root as `--cacert`, the OpenSSL build answers an `otherName` under a constraint of its type-id with verify result 51 `unsupported name constraint type`, as curl does.

## Context

Found in BL-6047 (ADR-1442). curl 8.18.0 with OpenSSL 3.5.5 answers 51 with the root as `--cacert` too. There the platform chain engine flags the refusal, and `OpenSslVerifyResult.KeepFirstNameConstraintRefusal` replaces flags only for a subtree refusal (47 or 48), so a chain flagged `HasNotSupportedNameConstraint` alone should be reported as 53 (ADR-1383; read from the code, not measured through the port on Linux). `CertificateNameConstraints.FindViolation` already returns `UnsupportedConstraintType`. The existing test `KeepFirstNameConstraintRefusal_NoSubtreeRefusalFlagged_TrustsTheChainEngine` pins today's behaviour.

## Acceptance criteria

- [ ] A chain flagged `HasNotSupportedNameConstraint` whose leaf holds a UPN under a root's UPN constraint is given code 51 by `OpenSslVerifyResult`, pinned in `NameConstraintVerifyResultTests`.
- [ ] A chain flagged `HasNotSupportedNameConstraint` for which `FindViolation` finds nothing still answers 53.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
