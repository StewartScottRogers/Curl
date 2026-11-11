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
completed: 2026-11-11
---
# BL-4985 — Fix flaky CI test job Pack Curl.Mcp (ubuntu-latest) step Run actions/upload-artifact@v4 that failed once on Linux

## Goal

`job Pack Curl.Mcp (ubuntu-latest) step Run actions/upload-artifact@v4` passes on every run on Windows, Linux and macOS; it failed once on Linux in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Pack Curl.Mcp (ubuntu-latest) step Run actions/upload-artifact@v4` failed on Linux in CI run 43094574123 (https://github.com/StewartScottRogers/Curl/actions/runs/43094574123). First failing commit: c8eedf57.

    if-no-files-found: error

Lanes test only on Windows, so reproduce with `gh run view 43094574123 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `job Pack Curl.Mcp (ubuntu-latest) step Run actions/upload-artifact@v4` fail intermittently is named under Notes and removed.
- [x] `job Pack Curl.Mcp (ubuntu-latest) step Run actions/upload-artifact@v4` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: not a repository defect. The upload step reached GitHub's artifact service, which answered every CreateArtifact with an HTML page (`Unexpected token '<', "<!DOCTYPE "... is not valid JSON`) on all 5 attempts, and at the same time failed the same way on Pack Curl.Mcp (macos-latest) and Quality gates shard 2 (windows-latest): a GitHub Actions artifact-service outage. The pack produced its one .nupkg and the path glob matched it ("1 file uploaded"), so `if-no-files-found: error` was not the cause.
- Nothing to remove: ci.yml is a guard file a lane may not change, and a retry wrapper around upload-artifact would only hide an outage. Criteria ticked on that basis; later runs pass without a change.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: b6f7709b bff1d2aa.
- 2026-11-11: Doing -> Done. GitHub artifact service outage, not a repo defect
