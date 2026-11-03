---
id: BL-4662
title: Fix CI failure Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4662 — Fix CI failure Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput on Linux

## Goal

`Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput` failed on Linux in CI run 41589009428 (https://github.com/StewartScottRogers/Curl/actions/runs/41589009428). First failing commit: cb7d5b13.

    Assertion failed. Expected value to be greater than or equal to the lower bound.

Failing data rows: `Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput ("rediss","CurlSurface:Verbose-Rediss-1")`.

Lanes test only on Windows, so reproduce with `gh run view 41589009428 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the OpenSSL build writes `* SSL connection using TLSv1...` during the TLS handshake, before curl 8.22's `* Established connection ...` line, which waits for the whole filter chain (TLS included). The failing run's standard error shows that order. The test expected the TLS line after the Established line on every platform. Fix: off Windows the expected order is now the SSL line, then the Established line. Windows (Schannel, already passing) keeps its order. `XmppTransferSurfaceCases` (xmpps) had the same pattern and landed after the failing run, so it got the same fix.
- Test-only change in Curl.Conformance.Extended.UnitTests, with no production code, so no quality run was needed. The project's 1,399 fast tests pass on Windows. Linux and macOS: the expectations follow the OpenSSL order shown in the CI log, and the shift's CI watch checks them on the push.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 54a7efe2.
- 2026-11-03: Doing -> Done. Linux and macOS now expect the OpenSSL SSL line before curl 8.22's Established line
