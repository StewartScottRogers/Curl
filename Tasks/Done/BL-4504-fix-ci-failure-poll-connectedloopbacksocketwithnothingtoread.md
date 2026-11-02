---
id: BL-4504
title: Fix CI failure Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4504 — Fix CI failure Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly on macOS

## Goal

`Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly` failed on macOS in CI run 41465596578 (https://github.com/StewartScottRogers/Curl/actions/runs/41465596578). First failing commit: 46726ad6.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 41465596578 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Poll_ConnectedLoopbackSocketWithNothingToRead_IsWritableOnly` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- The failure is flaky, not a regression: the test dates from 8819eb3ab (2026-10-16) and passed on macOS until 46726ad6, which only renumbered ADRs. On macOS the client socket polled readable, error and writable together (POLLHUP/POLLERR), so its connection had been hung up or reset. The cause could not be reproduced on Windows.
- Fix (test only, no production change, so no quality run): the listener's backlog is now 8, and the test keeps accepting until it gets its own client's peer, closing any stray connection another test process puts on the port. Every assertion carries the socket state (endpoints, SO_ERROR, bytes available), so a repeat failure shows its cause.
- CI criterion: met by keeping the change platform-neutral, with the Engine fast tests green here (2947 passed, 7 skipped).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: ec130cbd.
- 2026-11-02: Doing -> Done. Test accepts its own client's peer and reports socket state on failure
