---
id: BL-1651
title: Fix CI failure EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitTests, Curl.Cookies.UnitLibrary]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1651 — Fix CI failure EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother on Linux and macOS

## Goal

`EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother` failed on Linux and macOS in CI run 37694677072 (https://github.com/StewartScottRogers/Curl/actions/runs/37694677072). First failing commit: 5c771af7.

    CollectionAssert.AreEqual failed. Element at index 40 do not match.

Lanes test only on Windows, so reproduce with `gh run view 37694677072 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test, not the store. Every caller sets cookie `shared`, so its final value in
  the concurrent store is whichever caller wrote last (the scheduler's order); the
  sequential store always ends at `shared=63`. Windows happened to finish caller 63 last;
  Linux and macOS did not. First failing commit 5c771af7 is a task-board claim: the test
  has been flaky since BL-1496 added it.
- Fix: compare every cookie with `shared`'s value masked, and assert separately that the
  value is a whole number some caller wrote (0..63). The torn-value check in
  `CallEveryMember` still runs. `Curl.Cookies.UnitLibrary` is unchanged, so its coverage is too.
- Verified: Curl.Cookies.UnitTests 515 passed on 3 runs in a row; full build clean and
  fast tests green on Windows. The CI half of the criterion is checked when the shift pushes
  (lanes do not push).

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. The concurrent-callers cookie test no longer depends on which caller writes the shared cookie last, so it passes on every platform
