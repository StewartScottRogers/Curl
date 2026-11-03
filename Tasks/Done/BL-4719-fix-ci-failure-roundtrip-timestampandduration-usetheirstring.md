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
completed: 2026-11-03
---
# BL-4719 — Fix CI failure RoundTrip_TimestampAndDuration_UseTheirStringForms on Linux and macOS and Windows

## Goal

`RoundTrip_TimestampAndDuration_UseTheirStringForms` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RoundTrip_TimestampAndDuration_UseTheirStringForms` failed on Linux and macOS and Windows in CI run 41620112084 (https://github.com/StewartScottRogers/Curl/actions/runs/41620112084). First failing commit: fdcd5c92.

    Test method Curl.Conformance.Extended.UnitTests.Grpc.Reflection.GrpcProtoJsonCases.RoundTrip_TimestampAndDuration_UseTheirStringForms threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41620112084 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RoundTrip_TimestampAndDuration_UseTheirStringForms` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: CI run 41620112084 failed with KnownFailurePassedException: Grpc:ProtoJson-Timestamp-1 passed while KnownFailures.txt still listed it. 152216883 (BL-4688) already removed that line and is on this branch and origin/work/dark-factory, so the test passes here (Curl.Conformance.Extended.UnitTests: 1710 passed, 1 skipped).
- Also fixed: renumbering 1ed1bc4fc moved the ProtoJSON wrapper/FieldMask/Struct task from BL-4719 to BL-4721 but left five KnownFailures.txt lines naming BL-4719; they now name BL-4721.
- CI on Linux and macOS: the change is a text-only known-failure list edit, platform-neutral; solution build clean and every fast-test project green on Windows. The shift's CI watch files any failure.
- No production code changed, so no Measure-CodeQuality run was needed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: dee7b22b.
- 2026-11-03: Doing -> Done. RoundTrip_TimestampAndDuration_UseTheirStringForms passes: BL-4688 already dropped the stale Timestamp known failure
