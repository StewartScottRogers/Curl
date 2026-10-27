---
id: BL-3909
title: Fix CI failure test3207 on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-26
completed:
---
# BL-3909 — Fix CI failure test3207 on Linux

## Goal

`test3207` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test3207` failed on Linux in CI run 40433730485 (https://github.com/StewartScottRogers/Curl/actions/runs/40433730485). First failing commit: 6566f195.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 40433730485 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test3207` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 1028af8c.
