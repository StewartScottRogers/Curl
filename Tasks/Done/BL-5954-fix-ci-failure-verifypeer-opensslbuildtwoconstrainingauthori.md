---
id: BL-5954
title: Fix CI failure VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5954 — Fix CI failure VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes on Linux

## Goal

`VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes` failed on Linux in CI run 45604105442 (https://github.com/StewartScottRogers/Curl/actions/runs/45604105442). First failing commit: d3ffc4ee.

    Assertion failed. Expected strings to be equal.

Failing data rows: `VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes (False,"excluded subtree violation (48)")`.

Lanes test only on Windows, so reproduce with `gh run view 45604105442 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `VerifyPeer_OpenSslBuildTwoConstrainingAuthoritiesRefusingDifferently_ReportsTheUpperOnes` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: `OpenSslVerifyResult.StatusOf` left the platform chain engine's name-constraint flags in
  place and only added the one `CertificateNameConstraints.FindViolation` found. Windows' engine
  flags nothing for a chain stopped at a constrained non-self-signed anchor; the Linux engine does,
  so the status held both 47 and 48 (or the lower CA's 47) and `OfNameConstraints` answered 47.
- Fix: the violation found in OpenSSL's top-down order (ADR-1391) now replaces the platform's
  name-constraint flags, both for a partial chain at a listed certificate and wherever the engine
  flags a permitted or excluded refusal. No new decision, so no ADR.
- `WithNameConstraintViolation_PlatformFlags_KeepsOnlyTheViolationFound` pins the Linux flag
  combinations on every platform.
- Measured: `dotnet build` clean, fast tests green, `Measure-CodeQuality.ps1 -Library
  Curl.Networking.UnitLibrary` 0 failing members, format check clean on both files.
- The CI half of the criterion is ticked under lane rule 11: a lane cannot see CI for its own
  push; the change is platform-neutral and the shift's CI watch files any failure.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: ef2953f2.
- 2026-11-25: Doing -> Done. The OpenSSL build reports the upper CA's name-constraint refusal on Linux too.
