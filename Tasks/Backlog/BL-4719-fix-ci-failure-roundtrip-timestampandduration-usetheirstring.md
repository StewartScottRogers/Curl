---
id: BL-4719
title: Fix CI failure RoundTrip_TimestampAndDuration_UseTheirStringForms on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4719 — Fix CI failure RoundTrip_TimestampAndDuration_UseTheirStringForms on Linux and macOS and Windows

## Goal

`RoundTrip_TimestampAndDuration_UseTheirStringForms` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RoundTrip_TimestampAndDuration_UseTheirStringForms` failed on Linux and macOS and Windows in CI run 41620112084 (https://github.com/StewartScottRogers/Curl/actions/runs/41620112084). First failing commit: fdcd5c92.

    Test method Curl.Conformance.Extended.UnitTests.Grpc.Reflection.GrpcProtoJsonCases.RoundTrip_TimestampAndDuration_UseTheirStringForms threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41620112084 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RoundTrip_TimestampAndDuration_UseTheirStringForms` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-03: Created.
