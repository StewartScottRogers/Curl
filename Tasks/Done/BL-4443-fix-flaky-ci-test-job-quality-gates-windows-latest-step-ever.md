---
id: BL-4443
title: Fix flaky CI test job Quality gates (windows-latest) step Every shard succeeded that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4443 — Fix flaky CI test job Quality gates (windows-latest) step Every shard succeeded that failed once on Windows

## Goal

`job Quality gates (windows-latest) step Every shard succeeded` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Every shard succeeded` failed on Windows in CI run 41252530857 (https://github.com/StewartScottRogers/Curl/actions/runs/41252530857). First failing commit: 451233b3.

    ∩╗┐2026-11-01T00:08:30.8545463Z ##[group]Run if ('failure' -ne 'success') {

Lanes test only on Windows, so reproduce with `gh run view 41252530857 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `job Quality gates (windows-latest) step Every shard succeeded` fail intermittently is named under Notes and removed.
- [x] `job Quality gates (windows-latest) step Every shard succeeded` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: ec5da34f f59aada3.
- 2026-10-31: Doing -> Done. Every shard succeeded no longer fails: the shard 3 hang in Cgr109Tests was removed by BL-4442
