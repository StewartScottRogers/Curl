---
id: BL-4781
title: Fix CI failure IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4781 — Fix CI failure IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames on Windows

## Goal

`IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames` failed on Windows in CI run 41735630431 (https://github.com/StewartScottRogers/Curl/actions/runs/41735630431). First failing commit: fad49f8f.

    Assertion failed. Expected value to be greater than or equal to the lower bound.

Lanes test only on Windows, so reproduce with `gh run view 41735630431 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a real-time race, not a regression (first failing commit fad49f8f only touched a task file). The case runs
  curl on real time against the reference container's 2500 ms disposition delay and asked for at least two empty
  frames. On the loaded CI runner the thread pool let only one of the five 500 ms heartbeat ticks through (actual: 1).
- Fix (test only): the case now asks for at least one empty frame after the transfer. That matches its own failure
  message ("curl sent no empty frames"). The exact 500 ms cadence is already pinned on a fake TimeProvider by
  `Amqp1ClientFlowTests.RunAsync_PeerIdleTimeOut1000ms_SendsAnEmptyFrameEvery500msWhileWaitingForTheOutcome_Part2Section2_4_5`,
  so no coverage is lost. Decided as the sensible default; no production code changed, so no quality run.
- Platform-neutral: no paths, OS text or certificates. Curl.Conformance.Extended.UnitTests is green here
  (1719 passed, 1 skipped). CI on Linux and macOS is left to the shift's CI watch (rule 11).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b62019a4.
