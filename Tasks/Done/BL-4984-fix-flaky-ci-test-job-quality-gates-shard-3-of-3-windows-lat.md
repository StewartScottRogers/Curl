---
id: BL-4984
title: Fix flaky CI test job Quality gates shard 3 of 3 (windows-latest) step Run actions/upload-artifact@v4 that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4984 — Fix flaky CI test job Quality gates shard 3 of 3 (windows-latest) step Run actions/upload-artifact@v4 that failed once on Windows

## Goal

`job Quality gates shard 3 of 3 (windows-latest) step Run actions/upload-artifact@v4` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates shard 3 of 3 (windows-latest) step Run actions/upload-artifact@v4` failed on Windows in CI run 43094574123 (https://github.com/StewartScottRogers/Curl/actions/runs/43094574123). First failing commit: c8eedf57.

    if-no-files-found: error

Lanes test only on Windows, so reproduce with `gh run view 43094574123 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `job Quality gates shard 3 of 3 (windows-latest) step Run actions/upload-artifact@v4` fail intermittently is named under Notes and removed.
- [x] `job Quality gates shard 3 of 3 (windows-latest) step Run actions/upload-artifact@v4` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: not a defect in the repository. Run 43094574123 lost the artifact upload in several unrelated jobs at once (shard 2 Windows, Pack Curl.Mcp macOS, this shard): every attempt got an HTML page (`Unexpected token <, <!DOCTYPE`) instead of JSON from GitHub's artifact service, five retries over about 10 minutes each - a GitHub Actions outage. The shard had already collected its files (39 found). Nothing in Measure-CodeQuality.ps1 can change that, and ci.yml is off limits to a lane. No code changed; the upload step passes on the runs either side. Platform-neutral criterion met by no change.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: e0333b23 5ec017e2.
- 2026-11-11: Doing -> Done. Upload failure was a GitHub artifact service outage (HTML instead of JSON, several jobs at once); nothing in the repository to fix
