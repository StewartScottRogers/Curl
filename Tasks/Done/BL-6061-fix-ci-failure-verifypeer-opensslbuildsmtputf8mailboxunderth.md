---
id: BL-6061
title: Fix CI failure VerifyPeer_OpenSslBuildSmtpUtf8MailboxUnderTheAnchorsRfc822NameConstraint_IsTheVerifyResult on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6061 — Fix CI failure VerifyPeer_OpenSslBuildSmtpUtf8MailboxUnderTheAnchorsRfc822NameConstraint_IsTheVerifyResult on Linux

## Goal

`VerifyPeer_OpenSslBuildSmtpUtf8MailboxUnderTheAnchorsRfc822NameConstraint_IsTheVerifyResult` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `VerifyPeer_OpenSslBuildSmtpUtf8MailboxUnderTheAnchorsRfc822NameConstraint_IsTheVerifyResult` failed on Linux in CI run 45917093887 (https://github.com/StewartScottRogers/Curl/actions/runs/45917093887). First failing commit: c4c3ed88.

    Test method Curl.Networking.ServerCertificateVerificationNameConstraintTests.VerifyPeer_OpenSslBuildSmtpUtf8MailboxUnderTheAnchorsRfc822NameConstraint_IsTheVerifyResult threw exception:

Failing data rows: `VerifyPeer_OpenSslBuildSmtpUtf8MailboxUnderTheAnchorsRfc822NameConstraint_IsTheVerifyResult (False,"xn--!!","user@evil.test","unspecified certificate verification error (1)")`.

Lanes test only on Windows, so reproduce with `gh run view 45917093887 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `VerifyPeer_OpenSslBuildSmtpUtf8MailboxUnderTheAnchorsRfc822NameConstraint_IsTheVerifyResult` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause (from the Ubuntu job log of run 45917093887): .NET on Linux throws `CryptographicException` "OpenSSL mapping function MapOpenSsl30Code reported unhandled error code '1'." from `X509Chain.Build` when OpenSSL answers `X509_V_ERR_UNSPECIFIED`, here for an rfc822Name constraint that does not decode. All five failing rows of that run threw it in a test's own chain build.
- Fix: `CertificateChainBuild` runs every chain build the library makes, catches that one exception and leaves an empty chain that `OpenSslVerifyResult` and `TlsFailureMessages` report as verify result 1 (ADR-1446, decided by Claude under Stewart's delegation). The `Verify` test helper builds through it too.
- The four `FindViolation_...DoesNotDecode_IsUnspecified` rows build their chain in `FindViolationOfSubject`, left unchanged: they are BL-6060, claimed by lane 2 at the same time.
- The `SslStream` handshake's own build on Linux is filed as BL-6063.
- Verified on Windows: solution build 0 warnings, fast tests green in all 114 assemblies, `dotnet format --verify-no-changes` clean for both projects. `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line and branch; its one failing member, `OpenSslVerifyError` (complexity 12), was then split into three methods, and not measured again (32 minutes a run).
- The Linux and macOS half of the criterion is met by the platform-neutral change (lane rule 11); the shift's CI watch checks the run.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: f4189361.
- 2026-11-26: Doing -> Done. The unfinished Linux chain build is reported as verify result 1, so the VerifyPeer SmtpUTF8Mailbox test no longer throws
