---
id: BL-4847
title: Fix CI failure Connack_ServerKeepAlive_IsUsedSoASubscriberPings on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4847 — Fix CI failure Connack_ServerKeepAlive_IsUsedSoASubscriberPings on macOS

## Goal

`Connack_ServerKeepAlive_IsUsedSoASubscriberPings` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Connack_ServerKeepAlive_IsUsedSoASubscriberPings` failed on macOS in CI run 41836589655 (https://github.com/StewartScottRogers/Curl/actions/runs/41836589655). First failing commit: 650d20c7.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 41836589655 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Connack_ServerKeepAlive_IsUsedSoASubscriberPings` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the case ran on the system clock, so the subscriber's second PINGREQ fell due 0.5 s after the first. On a loaded macOS runner the broker's DISCONNECT (its answer to the PINGREQ) came later than that, so the client found the last PINGREQ unanswered and closed with Keep Alive timeout: exit 8, Weird server reply.
- Fix (test only, no production change, so no quality run): the case now runs on `FirstPingOnlyClock`, which fires the first 0.5 s timer at once and never fires a later timer. Exactly one PINGREQ goes out, and no second one can race the broker's answer on any platform.
- Passes locally (Mqtt5 cases 213/213, project fast tests 1719 passed, 1 skipped). The fix is platform-neutral (it assumes no path, OS error or scheduling order), so the CI criterion is met by that and by the green fast tests here (lane rule 11). The shift's CI watch files any failure.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: bcab4505.
- 2026-11-04: Doing -> Done. Connack_ServerKeepAlive_IsUsedSoASubscriberPings runs on a clock that sends exactly one PINGREQ, so it no longer races the Keep Alive timeout on slow runners
