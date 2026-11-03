---
id: BL-4725
title: Fix CI failure RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray on Linux and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-11-03
completed:
---
# BL-4725 — Fix CI failure RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray on Linux and Windows

## Goal

`RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray` failed on Linux and Windows in CI run 41632220793 (https://github.com/StewartScottRogers/Curl/actions/runs/41632220793). First failing commit: 27b710a0.

    Test method Curl.Conformance.Extended.UnitTests.Grpc.Reflection.GrpcProtoJsonWellKnownCases.RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41632220793 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-03: Created.
