---
id: BL-3877
title: Fix CI failure Format_OpenSslRareGeneralNames_PrintsThemAsOpenSsl on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitTests, Curl.Output.UnitLibrary]
requirement: none
created: 2026-10-26
completed:
---
# BL-3877 — Fix CI failure Format_OpenSslRareGeneralNames_PrintsThemAsOpenSsl on Linux and macOS

## Goal

`Format_OpenSslRareGeneralNames_PrintsThemAsOpenSsl` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Format_OpenSslRareGeneralNames_PrintsThemAsOpenSsl` failed on Linux and macOS in CI run 40402353061 (https://github.com/StewartScottRogers/Curl/actions/runs/40402353061). First failing commit: 9f111fac.

    Assertion failed. Expected string to contain the specified substring.

Lanes test only on Windows, so reproduce with `gh run view 40402353061 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Format_OpenSslRareGeneralNames_PrintsThemAsOpenSsl` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-26: Created.
