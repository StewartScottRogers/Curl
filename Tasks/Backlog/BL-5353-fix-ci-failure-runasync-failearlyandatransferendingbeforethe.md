---
id: BL-5353
title: Fix CI failure RunAsync_FailEarlyAndATransferEndingBeforeTheAbortLands_CompletesIt on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-11-17
completed:
---
# BL-5353 — Fix CI failure RunAsync_FailEarlyAndATransferEndingBeforeTheAbortLands_CompletesIt on Linux

## Goal

`RunAsync_FailEarlyAndATransferEndingBeforeTheAbortLands_CompletesIt` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_FailEarlyAndATransferEndingBeforeTheAbortLands_CompletesIt` failed on Linux in CI run 44077672653 (https://github.com/StewartScottRogers/Curl/actions/runs/44077672653). First failing commit: 4d631734.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 44077672653 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_FailEarlyAndATransferEndingBeforeTheAbortLands_CompletesIt` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-17: Created.
