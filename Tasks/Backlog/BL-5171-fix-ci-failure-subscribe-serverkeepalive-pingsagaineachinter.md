---
id: BL-5171
title: Fix CI failure Subscribe_ServerKeepAlive_PingsAgainEachIntervalAndAcceptsEachPingresp on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5171 — Fix CI failure Subscribe_ServerKeepAlive_PingsAgainEachIntervalAndAcceptsEachPingresp on Windows

## Goal

`Subscribe_ServerKeepAlive_PingsAgainEachIntervalAndAcceptsEachPingresp` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Subscribe_ServerKeepAlive_PingsAgainEachIntervalAndAcceptsEachPingresp` failed on Windows in CI run 43574261401 (https://github.com/StewartScottRogers/Curl/actions/runs/43574261401). First failing commit: e671ba59.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 43574261401 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Subscribe_ServerKeepAlive_PingsAgainEachIntervalAndAcceptsEachPingresp` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-14: Created.
