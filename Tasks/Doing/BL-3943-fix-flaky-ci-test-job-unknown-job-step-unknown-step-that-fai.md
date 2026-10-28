---
id: BL-3943
title: Fix flaky CI test job unknown job step unknown step that failed once on unknown job
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-10-28
completed:
---
# BL-3943 — Fix flaky CI test job unknown job step unknown step that failed once on unknown job

## Goal

`job unknown job step unknown step` passes on every run on Windows, Linux and macOS; it failed once on unknown job in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job unknown job step unknown step` failed on unknown job in CI run 40709122071 (https://github.com/StewartScottRogers/Curl/actions/runs/40709122071). First failing commit: 49687722.

    the run failed and gh run view --log-failed printed no line

Lanes test only on Windows, so reproduce with `gh run view 40709122071 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `job unknown job step unknown step` fail intermittently is named under Notes and removed.
- [ ] `job unknown job step unknown step` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 024f206a 35faa999.
