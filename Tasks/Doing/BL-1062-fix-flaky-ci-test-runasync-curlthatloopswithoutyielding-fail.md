---
id: BL-1062
title: Fix flaky CI test RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-09-30
completed:
---
# BL-1062 — Fix flaky CI test RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer that failed once on Windows

## Goal

`RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer` failed on Windows in CI run 36797838233 (https://github.com/StewartScottRogers/Curl/actions/runs/36797838233). First failing commit: d065d6d3.

    Test method Curl.Conformance.UpstreamCaseRunnerTests.RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer threw exception:

Lanes test only on Windows, so reproduce with `gh run view 36797838233 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer` fail intermittently is named under Notes and removed.
- [ ] `RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
