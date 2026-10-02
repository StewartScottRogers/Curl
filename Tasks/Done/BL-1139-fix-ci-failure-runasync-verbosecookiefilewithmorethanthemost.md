---
id: BL-1139
title: Fix CI failure RunAsync_VerboseCookieFileWithMoreThanTheMostCookiesSent_PrintsCurlsLimitLineBeforeTheRequest on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1139 — Fix CI failure RunAsync_VerboseCookieFileWithMoreThanTheMostCookiesSent_PrintsCurlsLimitLineBeforeTheRequest on Linux and macOS

## Goal

`RunAsync_VerboseCookieFileWithMoreThanTheMostCookiesSent_PrintsCurlsLimitLineBeforeTheRequest` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_VerboseCookieFileWithMoreThanTheMostCookiesSent_PrintsCurlsLimitLineBeforeTheRequest` failed on Linux and macOS in CI run 36957169050 (https://github.com/StewartScottRogers/Curl/actions/runs/36957169050). First failing commit: 99330d44.

    Assertion failed. Expected string to contain the specified substring.

Lanes test only on Windows, so reproduce with `gh run view 36957169050 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_VerboseCookieFileWithMoreThanTheMostCookiesSent_PrintsCurlsLimitLineBeforeTheRequest` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test passed its expected text through `NativeLines`, turning `
` into `Environment.NewLine`, but the runner writes the `-v` lines with `
` on every platform (CI run 36957169050 shows `* using HTTP/1.x
` and `> GET / HTTP/1.1
` on Linux and macOS, the same bytes as on Windows). The test now pins those literal bytes, like its neighbour `RunAsync_VerboseCookieFileWithRefusedSetCookieLines_PrintsCurlsRefusalLines` does. Passes locally on Windows; the expected string is exactly the Linux and macOS actual from the CI log, so CI will confirm it on the commit that lands.
- The sibling failure in the same file, `RunAsync_VerboseCookieFileLongerThanTheLongestCookieHeader_PrintsCurlsRestrictedLineBeforeTheRequest`, has the same cause and is left to its own task, BL-1140.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. The cookie-limit -v test pins curl's CRLF bytes, which are the same on Windows, Linux and macOS
