---
id: BL-1092
title: Fix CI failure RunAsync_VerboseUidFetchWithStartTls_WritesTheSessionLinesAroundTheUpgrade on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1092 — Fix CI failure RunAsync_VerboseUidFetchWithStartTls_WritesTheSessionLinesAroundTheUpgrade on Linux and macOS

## Goal

`RunAsync_VerboseUidFetchWithStartTls_WritesTheSessionLinesAroundTheUpgrade` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_VerboseUidFetchWithStartTls_WritesTheSessionLinesAroundTheUpgrade` failed on Linux and macOS in CI run 36920828630 (https://github.com/StewartScottRogers/Curl/actions/runs/36920828630). First failing commit: 7a6da1e5.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 36920828630 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_VerboseUidFetchWithStartTls_WritesTheSessionLinesAroundTheUpgrade` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Already fixed by BL-1090 (commit 53190660, "pin STARTTLS verbose TLS lines per platform"): the test was split into `RunAsync_VerboseUidFetchWithStartTlsOnWindows_WritesTheSchannelLinesAroundTheUpgrade` (Windows, schannel lines) and `RunAsync_VerboseUidFetchWithStartTlsOffWindows_WritesTheOpenSslTrustLineAroundTheUpgrade` (Linux and macOS, OpenSSL "SSL Trust" line). The failing run 36920828630 was on 7a6da1e5, before the fix.
- CI run 36922316653 on c8beac60, which contains 53190660, passed on Windows, Linux and macOS. Locally both halves pass (the off-Windows one is skipped on Windows, as intended); full fast suite green. No code change needed.
- The sibling POP3 failure (BL-1093) and the SMTP one were fixed by the same commit.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. The STARTTLS IMAP verbose test passes on all three platforms; fixed by BL-1090's per-platform split, confirmed by green CI run 36922316653
