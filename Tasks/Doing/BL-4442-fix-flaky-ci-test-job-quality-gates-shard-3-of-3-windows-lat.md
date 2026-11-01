---
id: BL-4442
title: Fix flaky CI test job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3 that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-10-31
completed:
---
# BL-4442 — Fix flaky CI test job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3 that failed once on Windows

## Goal

`job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3` failed on Windows in CI run 41252530857 (https://github.com/StewartScottRogers/Curl/actions/runs/41252530857). First failing commit: 451233b3.

    VSTEST_CONNECTION_TIMEOUT: 600

Lanes test only on Windows, so reproduce with `gh run view 41252530857 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3` fail intermittently is named under Notes and removed.
- [ ] `job Quality gates shard 3 of 3 (windows-latest) step Collect coverage for shard 3` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: db83650e 4f733ff7.
