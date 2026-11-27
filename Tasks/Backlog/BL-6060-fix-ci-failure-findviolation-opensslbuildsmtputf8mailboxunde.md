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
completed:
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

- [ ] `FindViolation_OpenSslBuildSmtpUtf8MailboxUnderAConstraintThatDoesNotDecode_IsUnspecified` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-26: Created.
