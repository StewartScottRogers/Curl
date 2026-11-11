---
id: BL-4985
title: Fix flaky CI test job Pack Curl.Mcp (ubuntu-latest) step Run actions/upload-artifact@v4 that failed once on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-11-11
completed:
---
# BL-4985 — Fix flaky CI test job Pack Curl.Mcp (ubuntu-latest) step Run actions/upload-artifact@v4 that failed once on Linux

## Goal

`job Pack Curl.Mcp (ubuntu-latest) step Run actions/upload-artifact@v4` passes on every run on Windows, Linux and macOS; it failed once on Linux in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Pack Curl.Mcp (ubuntu-latest) step Run actions/upload-artifact@v4` failed on Linux in CI run 43094574123 (https://github.com/StewartScottRogers/Curl/actions/runs/43094574123). First failing commit: c8eedf57.

    if-no-files-found: error

Lanes test only on Windows, so reproduce with `gh run view 43094574123 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `job Pack Curl.Mcp (ubuntu-latest) step Run actions/upload-artifact@v4` fail intermittently is named under Notes and removed.
- [ ] `job Pack Curl.Mcp (ubuntu-latest) step Run actions/upload-artifact@v4` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: b6f7709b bff1d2aa.
