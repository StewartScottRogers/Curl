---
id: BL-5919
title: Fix CI failure job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2 on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-24
completed:
---
# BL-5919 — Fix CI failure job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2 on Windows

## Goal

`job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2` failed on Windows in CI run 45520782590 (https://github.com/StewartScottRogers/Curl/actions/runs/45520782590). First failing commit: 0c6497ad.

    VSTEST_CONNECTION_TIMEOUT: 600

Lanes test only on Windows, so reproduce with `gh run view 45520782590 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-24: Created.
