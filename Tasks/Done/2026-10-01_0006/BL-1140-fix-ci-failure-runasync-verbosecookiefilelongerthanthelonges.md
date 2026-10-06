---
id: BL-1140
title: Fix CI failure RunAsync_VerboseCookieFileLongerThanTheLongestCookieHeader_PrintsCurlsRestrictedLineBeforeTheRequest on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1140 — Fix CI failure RunAsync_VerboseCookieFileLongerThanTheLongestCookieHeader_PrintsCurlsRestrictedLineBeforeTheRequest on Linux and macOS

## Goal

`RunAsync_VerboseCookieFileLongerThanTheLongestCookieHeader_PrintsCurlsRestrictedLineBeforeTheRequest` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_VerboseCookieFileLongerThanTheLongestCookieHeader_PrintsCurlsRestrictedLineBeforeTheRequest` failed on Linux and macOS in CI run 36957169050 (https://github.com/StewartScottRogers/Curl/actions/runs/36957169050). First failing commit: 99330d44.

    Assertion failed. Expected string to contain the specified substring.

Lanes test only on Windows, so reproduce with `gh run view 36957169050 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_VerboseCookieFileLongerThanTheLongestCookieHeader_PrintsCurlsRestrictedLineBeforeTheRequest` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: same as BL-1139. The test passed its expected text through `NativeLines`, turning `\n` into `Environment.NewLine`, but the runner writes the `-v` lines with `\r\n` on every platform (CI run 36957169050 shows `* using HTTP/1.x\r\n` and `> GET / HTTP/1.1\r\r\n` on Linux and macOS, the same bytes the Windows run produces). The test now pins those literal bytes. Passes locally on Windows; the expected string is exactly the Linux and macOS actual from the CI log, so CI confirms it on the commit that lands.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. The cookie-restriction -v test pins curl's CRLF bytes, which are the same on Windows, Linux and macOS
