---
id: BL-4724
title: Fix CI failure RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero on Linux and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4724 — Fix CI failure RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero on Linux and Windows

## Goal

`RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero` failed on Linux and Windows in CI run 41632220793 (https://github.com/StewartScottRogers/Curl/actions/runs/41632220793). First failing commit: 27b710a0.

    Test method Curl.Conformance.Extended.UnitTests.Grpc.Reflection.GrpcProtoJsonWellKnownCases.RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41632220793 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 3e490659.
- 2026-11-03: Doing -> Done. Already fixed by 1df901115, which drops the stale ProtoJSON known-failure lines; test passes locally
