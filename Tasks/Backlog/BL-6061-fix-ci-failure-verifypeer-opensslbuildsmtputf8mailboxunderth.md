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
completed:
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

- [ ] `VerifyPeer_OpenSslBuildSmtpUtf8MailboxUnderTheAnchorsRfc822NameConstraint_IsTheVerifyResult` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-26: Created.
