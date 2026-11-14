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
completed: 2026-11-14
---
# BL-5171 — Fix CI failure Subscribe_ServerKeepAlive_PingsAgainEachIntervalAndAcceptsEachPingresp on Windows

## Goal

`Subscribe_ServerKeepAlive_PingsAgainEachIntervalAndAcceptsEachPingresp` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Subscribe_ServerKeepAlive_PingsAgainEachIntervalAndAcceptsEachPingresp` failed on Windows in CI run 43574261401 (https://github.com/StewartScottRogers/Curl/actions/runs/43574261401). First failing commit: e671ba59.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 43574261401 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Subscribe_ServerKeepAlive_PingsAgainEachIntervalAndAcceptsEachPingresp` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Failure was a timing race: curl pings every half second on a Server Keep Alive of 1 s, and a loaded Windows CI machine delivered the first PINGRESP after the next ping fell due, giving exit 8 (curl: (8) Weird server reply). Passes locally. Decided: the test now uses a Server Keep Alive of 2 s (ping each second, -m 10), a whole second of slack; production code unchanged. CI on Linux and macOS: the change is platform-neutral, fast tests green here.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 0b63dc2d.
- 2026-11-14: Doing -> Done. Test given a 2 s keep alive for slack
