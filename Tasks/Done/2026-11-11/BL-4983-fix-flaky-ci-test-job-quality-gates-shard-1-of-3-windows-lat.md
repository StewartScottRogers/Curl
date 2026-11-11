---
id: BL-4983
title: Fix flaky CI test job Quality gates shard 1 of 3 (windows-latest) step Run actions/upload-artifact@v4 that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4983 — Fix flaky CI test job Quality gates shard 1 of 3 (windows-latest) step Run actions/upload-artifact@v4 that failed once on Windows

## Goal

`job Quality gates shard 1 of 3 (windows-latest) step Run actions/upload-artifact@v4` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates shard 1 of 3 (windows-latest) step Run actions/upload-artifact@v4` failed on Windows in CI run 43094574123 (https://github.com/StewartScottRogers/Curl/actions/runs/43094574123). First failing commit: c8eedf57.

    if-no-files-found: error

Lanes test only on Windows, so reproduce with `gh run view 43094574123 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `job Quality gates shard 1 of 3 (windows-latest) step Run actions/upload-artifact@v4` fail intermittently is named under Notes and removed.
- [x] `job Quality gates shard 1 of 3 (windows-latest) step Run actions/upload-artifact@v4` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: not a Curl defect. The upload-artifact step got an HTML page (`Unexpected token '<', "<!DOCTYPE"`) from GitHub's artifact service on all 5 attempts, in the same run also for Pack Curl.Mcp (macos-latest) and Quality gates shard 2 (windows): a GitHub Actions artifact-service outage at ~20:24-20:39 UTC on 2026-11-11. Measure-CodeQuality.ps1 produced its files ("39 files uploaded"), so there is nothing in the repository to remove; no change made. Both criteria are met by the run passing on the runs either side.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: d96c1adb 05150e09.
- 2026-11-11: Doing -> Done. GitHub artifact-service outage (HTML response to CreateArtifact on several jobs), not a repository defect; no change needed.
