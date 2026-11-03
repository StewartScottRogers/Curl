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
completed:
---
# BL-4605 — Fix CI failure GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable on Linux and macOS

## Goal

`GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable` failed on Linux and macOS in CI run 41567287491 (https://github.com/StewartScottRogers/Curl/actions/runs/41567287491). First failing commit: 0afc799c.

    Test method Curl.Conformance.Extended.UnitTests.Grpc.GrpcTransportCases.GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41567287491 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `GoAway_LastStreamBeforeTheCall_FailsTheCallAsUnavailable` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-02: Created.
