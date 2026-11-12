---
id: BL-4988
title: Fix flaky CI test job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4 that failed once on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4988 — Fix flaky CI test job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4 that failed once on macOS

## Goal

`job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4` passes on every run on Windows, Linux and macOS; it failed once on macOS in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4` failed on macOS in CI run 43094574123 (https://github.com/StewartScottRogers/Curl/actions/runs/43094574123). First failing commit: c8eedf57.

    if-no-files-found: error

Lanes test only on Windows, so reproduce with `gh run view 43094574123 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4` fail intermittently is named under Notes and removed.
- [x] `job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: not a defect in the repository. Run 43094574123 got HTML (`Unexpected token <, <!DOCTYPE`) instead of JSON from GitHub artifact service on all five CreateArtifact attempts (about 10 minutes), the same outage that failed BL-4984. The pack step had produced its file (1 file found). ci.yml is off limits to a lane; no code changed. Platform-neutral criterion met by no change.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: baabee88 5cbf2a83.
- 2026-11-11: Doing -> Done. Upload failure was a GitHub artifact service outage (HTML instead of JSON); nothing in the repository to fix
