---
id: BL-5058
title: Fix CI failure Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5058 — Fix CI failure Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing on Windows

## Goal

`Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing` failed on Windows in CI run 43328655141 (https://github.com/StewartScottRogers/Curl/actions/runs/43328655141). First failing commit: 6f330b8e.

    Assertion failed. Expected exception of exact type SocketException but no exception was thrown.

Lanes test only on Windows, so reproduce with `gh run view 43328655141 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: the test connected to the closed ephemeral port; a parallel test may own that port by then, so the connect succeeds. The test now checks the closed handle no longer names a socket. No production change.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: acb33600.
- 2026-11-12: Doing -> Done. Test no longer connects to a possibly reused port
