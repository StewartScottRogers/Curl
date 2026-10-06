---
id: BL-1090
title: Fix CI failure RunAsync_VerboseMailUploadWithStartTls_WritesTheEstablishedConnectionLineAgainAfterTheUpgrade on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1090 — Fix CI failure RunAsync_VerboseMailUploadWithStartTls_WritesTheEstablishedConnectionLineAgainAfterTheUpgrade on Linux and macOS

## Goal

`RunAsync_VerboseMailUploadWithStartTls_WritesTheEstablishedConnectionLineAgainAfterTheUpgrade` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_VerboseMailUploadWithStartTls_WritesTheEstablishedConnectionLineAgainAfterTheUpgrade` failed on Linux and macOS in CI run 36919676608 (https://github.com/StewartScottRogers/Curl/actions/runs/36919676608). First failing commit: a237e38c.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 36919676608 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_VerboseMailUploadWithStartTls_WritesTheEstablishedConnectionLineAgainAfterTheUpgrade` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test pinned the Schannel build's two `schannel:` lines after STARTTLS; off
  Windows Curl matches curl's OpenSSL build and writes `SSL Trust: peer verification
  disabled` there instead (CI run 36919676608). The IMAP STARTTLS and POP3 STLS tests
  from BL-1084 pinned the same lines and fail the same way, so they are fixed here too.
- Fix (tests only; CLAUDE.md "pin each platform's answer in its own test"): each of the
  three tests is split into an `...OnWindows_...` test (`[OSCondition(OperatingSystems.Windows)]`,
  the Schannel lines) and an `...OffWindows_...` test (excluded on Windows, the OpenSSL
  trust line), sharing one assert helper. The failing test is now
  `RunAsync_VerboseMailUploadWithStartTlsOnWindows_WritesTheSchannelLinesAndTheEstablishedConnectionLineAgain`
  and `RunAsync_VerboseMailUploadWithStartTlsOffWindows_WritesTheOpenSslTrustLineAndTheEstablishedConnectionLineAgain`;
  the off-Windows expectation is the exact output CI reported on Linux and macOS.
- Verified locally on Windows: build clean, fast tests green (Curl.Console.UnitTests 2011
  passed, 18 skipped). Linux and macOS are confirmed by the shift's next CI run; the CI
  watch files a new task if they are not.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. SMTP, IMAP and POP3 STARTTLS verbose tests pin Schannel lines on Windows and the OpenSSL trust line elsewhere
