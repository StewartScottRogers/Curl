---
id: BL-4989
title: Fix CI failure job unknown job step unknown step on unknown job
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-11-11
completed:
---
# BL-4989 — Fix CI failure job unknown job step unknown step on unknown job

## Goal

`job unknown job step unknown step` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job unknown job step unknown step` failed on unknown job in CI run 43094919125 (https://github.com/StewartScottRogers/Curl/actions/runs/43094919125). First failing commit: f2336355.

    the run failed and gh run view --log-failed printed no line

Lanes test only on Windows, so reproduce with `gh run view 43094919125 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job unknown job step unknown step` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: not a defect in the repository. Run 43094919125 completed as failure with no job concluded and `gh run view --log-failed` printing no line: GitHub Actions infrastructure failure, no log to reproduce from. ci.yml is off limits to a lane; no code changed. Platform-neutral criterion met by no change.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 15da4c4d.
