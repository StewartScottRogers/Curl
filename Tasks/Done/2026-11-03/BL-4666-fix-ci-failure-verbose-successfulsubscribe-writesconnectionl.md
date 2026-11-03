---
id: BL-4666
title: Fix CI failure Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4666 — Fix CI failure Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput on Linux and macOS

## Goal

`Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput` failed on Linux and macOS in CI run 41597701882 (https://github.com/StewartScottRogers/Curl/actions/runs/41597701882). First failing commit: 4e244606.

    Assertion failed. Expected value to be greater than or equal to the lower bound.

Failing data rows: `Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput ("mqtt5s","CurlSurface:Verbose-Mqtt5s-1")`.

Lanes test only on Windows, so reproduce with `gh run view 41597701882 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Verbose_SuccessfulSubscribe_WritesConnectionLinesToStandardErrorAndOnlyTheMessageToStandardOutput` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the mqtt5s case expected the OpenSSL build's "* SSL connection using TLSv1." line after "* Established connection to ...", but the OpenSSL build writes it before (CI log of run 41597701882), as the xmpp and redis cases already pin. Fixed by pinning that order on Linux and macOS and keeping the Schannel order on Windows. Test-only change, so no Measure-CodeQuality run. CI criterion: the change is platform-neutral (each platform keeps its own order) and the project's fast tests are green here (1494 passed); the shift's CI watch files any failure.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 7e7c622d.
- 2026-11-03: Doing -> Done. mqtt5s -v case pins the OpenSSL build's TLS line before the connection line off Windows
