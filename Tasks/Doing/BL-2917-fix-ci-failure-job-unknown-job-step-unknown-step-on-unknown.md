---
id: BL-2917
title: Fix CI failure job unknown job step unknown step on unknown job
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: []
requirement: none
created: 2026-10-21
completed:
---
# BL-2917 — Fix CI failure job unknown job step unknown step on unknown job

## Goal

`job unknown job step unknown step` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job unknown job step unknown step` failed on unknown job in CI run 39623036192 (https://github.com/StewartScottRogers/Curl/actions/runs/39623036192). First failing commit: cdc38efa.

    the run failed and gh run view --log-failed printed no line

Lanes test only on Windows, so reproduce with `gh run view 39623036192 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job unknown job step unknown step` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 15da4c4d.
