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
completed: 2026-11-26
---
# BL-6059 — Answer 51, not 53, for an otherName constraint under a root anchor in the OpenSSL build

## Goal

With a self-signed root as `--cacert`, the OpenSSL build answers an `otherName` under a constraint of its type-id with verify result 51 `unsupported name constraint type`, as curl does.

## Context

Found in BL-6047 (ADR-1442). curl 8.18.0 with OpenSSL 3.5.5 answers 51 with the root as `--cacert` too. There the platform chain engine flags the refusal, and `OpenSslVerifyResult.KeepFirstNameConstraintRefusal` replaces flags only for a subtree refusal (47 or 48), so a chain flagged `HasNotSupportedNameConstraint` alone should be reported as 53 (ADR-1383; read from the code, not measured through the port on Linux). `CertificateNameConstraints.FindViolation` already returns `UnsupportedConstraintType`. The existing test `KeepFirstNameConstraintRefusal_NoSubtreeRefusalFlagged_TrustsTheChainEngine` pins today's behaviour.

## Acceptance criteria

- [x] A chain flagged `HasNotSupportedNameConstraint` whose leaf holds a UPN under a root's UPN constraint is given code 51 by `OpenSslVerifyResult`, pinned in `NameConstraintVerifyResultTests`.
- [x] A chain flagged `HasNotSupportedNameConstraint` for which `FindViolation` finds nothing still answers 53.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- ADR-1448: `KeepFirstNameConstraintRefusal` replaces a lone `HasNotSupportedNameConstraint` only when
  `FindViolation`'s first refusal is the unsupported constraint type (51). A subtree violation the
  chain engine did not flag still leaves the engine's flag (53), as the existing test pinned (renamed
  `KeepFirstNameConstraintRefusal_UnsupportedFlaggedAndASubtreeViolationFound_TrustsTheChainEngine`).
- curl's answer was already measured with a root as `--cacert` (ADR-1442); not measured again, and the
  port was not measured on Linux. The new UPN test is excluded on macOS, whose chain engine is its own.
- Verified: `dotnet build` clean, fast tests pass, `dotnet format` clean on both files,
  `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 7a8f7aaa d5b44798 3d836d5e.
- 2026-11-26: Doing -> Done. The OpenSSL build answers 51 for an otherName under a root anchor's constraint of its type-id
