---
id: BL-4605
title: Fix CI failure GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4605 — Fix CI failure GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable on Linux and macOS

## Goal

`GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable` failed on Linux and macOS in CI run 41567287491 (https://github.com/StewartScottRogers/Curl/actions/runs/41567287491). First failing commit: 0afc799c.

    Test method Curl.Conformance.Extended.UnitTests.Grpc.GrpcTransportCases.GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41567287491 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- The failure was not a platform problem. It also failed on windows-latest in run 41567287491. The test threw KnownFailurePassedException because `Grpc:Http2-GoAway-1` (and `Grpc:Http2-Errors-1`) now pass, but their KnownFailures.txt lines were still there at 0afc799c.
- 8fa866b82 (BL-4594), which comes after 0afc799c, already removed both lines, so no code change is needed. Locally, Curl.Conformance.Extended.UnitTests passes 778/778, including all 67 Grpc cases.
- CI criterion: the fix touches no platform-dependent behaviour (it only deletes lines from a data file), and the fast tests pass here; the shift's CI watch files a task for any further failure.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 20bd686e.
- 2026-11-02: Doing -> Done. GoAway_LastStreamBeforeTheCall passes: 8fa866b82 (BL-4594) already dropped the stale Grpc known-failure lines
