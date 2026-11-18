---
id: BL-5416
title: Fix CI failure job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3 on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-17
completed:
---
# BL-5416 — Fix CI failure job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3 on Windows

## Goal

`job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3` failed on Windows in CI run 44224595726 (https://github.com/StewartScottRogers/Curl/actions/runs/44224595726). First failing commit: 10f2e1ba.

    VSTEST_CONNECTION_TIMEOUT: 600

Lanes test only on Windows, so reproduce with `gh run view 44224595726 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 5ef21b59.
