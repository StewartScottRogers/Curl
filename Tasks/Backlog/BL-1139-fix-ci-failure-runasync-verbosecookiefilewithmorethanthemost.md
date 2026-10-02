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
completed:
---
# BL-1139 — Fix CI failure RunAsync_VerboseCookieFileWithMoreThanTheMostCookiesSent_PrintsCurlsLimitLineBeforeTheRequest on Linux and macOS

## Goal

`RunAsync_VerboseCookieFileWithMoreThanTheMostCookiesSent_PrintsCurlsLimitLineBeforeTheRequest` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_VerboseCookieFileWithMoreThanTheMostCookiesSent_PrintsCurlsLimitLineBeforeTheRequest` failed on Linux and macOS in CI run 36957169050 (https://github.com/StewartScottRogers/Curl/actions/runs/36957169050). First failing commit: 99330d44.

    Assertion failed. Expected string to contain the specified substring.

Lanes test only on Windows, so reproduce with `gh run view 36957169050 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_VerboseCookieFileWithMoreThanTheMostCookiesSent_PrintsCurlsLimitLineBeforeTheRequest` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-01: Created.
