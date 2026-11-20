---
id: BL-5619
title: Fix CI failure ConnectAsync_SockoptReturnsErrorWithoutOpenSocket_ClosesTheSocketItSawAndMakesNoConnection on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-20
completed:
---
# BL-5619 — Fix CI failure ConnectAsync_SockoptReturnsErrorWithoutOpenSocket_ClosesTheSocketItSawAndMakesNoConnection on Windows

## Goal

`ConnectAsync_SockoptReturnsErrorWithoutOpenSocket_ClosesTheSocketItSawAndMakesNoConnection` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_SockoptReturnsErrorWithoutOpenSocket_ClosesTheSocketItSawAndMakesNoConnection` failed on Windows in CI run 44787815476 (https://github.com/StewartScottRogers/Curl/actions/runs/44787815476). First failing commit: a594660c.

    Test method Curl.Engine.Cgr145Tests.ConnectAsync_SockoptReturnsErrorWithoutOpenSocket_ClosesTheSocketItSawAndMakesNoConnection threw exception:

Lanes test only on Windows, so reproduce with `gh run view 44787815476 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ConnectAsync_SockoptReturnsErrorWithoutOpenSocket_ClosesTheSocketItSawAndMakesNoConnection` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-20: Created.
