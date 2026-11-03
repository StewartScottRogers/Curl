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

- [ ] `IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b62019a4.
