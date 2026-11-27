---
id: BL-6060
title: Fix CI failure FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6060 — Fix CI failure FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified on Linux

## Goal

`FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified` failed on Linux in CI run 45917093887 (https://github.com/StewartScottRogers/Curl/actions/runs/45917093887). First failing commit: c4c3ed88.

    Test method Curl.Networking.ServerCertificateVerificationNameConstraintTests.FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified threw exception:

Failing data rows: `FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified ("xn--!!.test",0)`, `FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified ("",256)`, `FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified (".xn--!!.test",0)`, `FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified (".",254)`.

Lanes test only on Windows, so reproduce with `gh run view 45917093887 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause (Ubuntu job log): the test built the chain with `X509Chain.Build`, which on Linux throws `CryptographicException` ("MapOpenSsl30Code reported unhandled error code '1'") because OpenSSL itself answers these constraints with `X509_V_ERR_UNSPECIFIED`. Curl's checker was never reached.
- Fix: `CertificateNameConstraints.FindViolationAmong` checks certificates in chain order; `FindViolation(chain)` calls it. The test hands it leaf, intermediate and root, with no platform chain build, so it runs the same code on every platform.
- CI criterion: a lane cannot see CI for its own push. Met by the platform-neutral change (no `X509Chain.Build` in the test) and the green local run; the shift's CI watch files a task for any failure.
- The sibling `VerifyPeer_...` failure of the same run is BL-6061's, in Doing on another lane; left to it.
- The product gap on Linux (such a chain ends as a handshake failure, not exit 60) is filed as BL-6062.
- Checks on Windows: `dotnet build` 0 warnings; `Curl.Networking.UnitTests` 4908 passed, 33 skipped; no failure in the fast run; `dotnet format --verify-no-changes` clean on both files; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 0 failing members. No ADR: behaviour is unchanged.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 0d11bf0d.
- 2026-11-26: Doing -> Done. The test checks the certificates without a platform chain build, so it no longer depends on .NET on Linux.
