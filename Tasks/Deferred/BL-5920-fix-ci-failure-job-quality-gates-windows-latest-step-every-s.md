---
id: BL-5920
title: Fix CI failure job Quality gates (windows-latest) step Every shard succeeded on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-5919]
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-24
completed:
---
# BL-5920 — Fix CI failure job Quality gates (windows-latest) step Every shard succeeded on Windows

## Goal

`job Quality gates (windows-latest) step Every shard succeeded` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Every shard succeeded` failed on Windows in CI run 45520782590 (https://github.com/StewartScottRogers/Curl/actions/runs/45520782590). First failing commit: 0c6497ad.

    ∩╗┐2026-11-25T01:06:12.5170688Z ##[group]Run if ('failure' -ne 'success') {

Lanes test only on Windows, so reproduce with `gh run view 45520782590 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Quality gates (windows-latest) step Every shard succeeded` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- 2026-11-24, lane 11: this step has no failure of its own. It runs
  `if ('failure' -ne 'success') { ... exit 1 }` to report that a Quality gates shard failed.
  In run 45520782590 the only failed shard was shard 2 of 3 (windows-latest): "Collect
  coverage for shard 2" threw at `Measure-CodeQuality.ps1:296` after `dotnet test` of
  `Curl.Console.UnitTests` ended with "Test host process crashed" (2026-11-25T01:00:00Z).
  BL-5919 (in Doing on another lane, same `touches`) is that failure, so this task depends
  on it. Nothing was edited or measured beyond reading the CI log. Once BL-5919 is Done,
  check that it covers the crash; then this task's criterion needs no further code.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 96723aaa.
- 2026-11-24: Doing -> Backlog. Waits for BL-5919: this gate step only reports that shard 2 (windows-latest) failed, which BL-5919 fixes
- 2026-11-24: Backlog -> Deferred. already fixed, CI green on 6490ce256
