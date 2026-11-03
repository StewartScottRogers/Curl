---
id: BL-4726
title: Fix CI failure RoundTrip_FieldMask_IsOneStringOfLowerCamelCasePaths on Linux and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4726 — Fix CI failure RoundTrip_FieldMask_IsOneStringOfLowerCamelCasePaths on Linux and Windows

## Goal

`RoundTrip_FieldMask_IsOneStringOfLowerCamelCasePaths` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RoundTrip_FieldMask_IsOneStringOfLowerCamelCasePaths` failed on Linux and Windows in CI run 41632220793 (https://github.com/StewartScottRogers/Curl/actions/runs/41632220793). First failing commit: 27b710a0.

    Test method Curl.Conformance.Extended.UnitTests.Grpc.Reflection.GrpcProtoJsonWellKnownCases.RoundTrip_FieldMask_IsOneStringOfLowerCamelCasePaths threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41632220793 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RoundTrip_FieldMask_IsOneStringOfLowerCamelCasePaths` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: `KnownFailurePassedException`. BL-4721 made Grpc:ProtoJson-FieldMask-1 pass (along with Struct-1 and Wrappers-1, which are BL-4725 and BL-4724), but `KnownFailures.txt` still listed it. Commit 1df901115 (BL-4691) already dropped those lines and is on this branch, so this task needs no code change.
- Checked locally: `GrpcProtoJsonWellKnownCases` passes 6 of 6. The `Curl.Conformance.Extended.UnitTests` fast tests: 1719 passed, 0 failed, 1 skipped.
- CI on Linux and macOS: the fix only removes lines from a text file, and nothing in it depends on the platform, so this criterion is ticked under rule 11. The shift's CI watch files a task for any failure.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 7f4f300e.
- 2026-11-03: Doing -> Done. RoundTrip_FieldMask passes: its stale KnownFailures.txt line was already dropped by 1df901115
