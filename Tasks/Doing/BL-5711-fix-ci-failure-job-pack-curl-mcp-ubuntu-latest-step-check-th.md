---
id: BL-5711
title: Fix CI failure job Pack Curl.Mcp (ubuntu-latest) step Check the packages on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-11-21
completed:
---
# BL-5711 — Fix CI failure job Pack Curl.Mcp (ubuntu-latest) step Check the packages on Linux

## Goal

`job Pack Curl.Mcp (ubuntu-latest) step Check the packages` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Pack Curl.Mcp (ubuntu-latest) step Check the packages` failed on Linux in CI run 44941565490 (https://github.com/StewartScottRogers/Curl/actions/runs/44941565490). First failing commit: c3c60a47.

    ##[error]Process completed with exit code 1.

Lanes test only on Windows, so reproduce with `gh run view 44941565490 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Pack Curl.Mcp (ubuntu-latest) step Check the packages` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 60a0b9b1.
