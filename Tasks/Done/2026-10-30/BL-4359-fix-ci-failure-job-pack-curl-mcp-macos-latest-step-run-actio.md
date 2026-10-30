---
id: BL-4359
title: Fix CI failure job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4 on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-10-30
completed: 2026-10-30
---
# BL-4359 — Fix CI failure job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4 on macOS

## Goal

`job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4` failed on macOS in CI run 41098931622 (https://github.com/StewartScottRogers/Curl/actions/runs/41098931622). First failing commit: 767c799e.

    if-no-files-found: error

Lanes test only on Windows, so reproduce with `gh run view 41098931622 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a temporary fault in GitHub's own service, not in Curl's code. In run 41098931622 the
  macOS Pack Curl.Mcp job built both packages and passed "Check the packages". The upload step
  found its one file ("there will be 1 file uploaded"), then GitHub's artifact service timed out
  on `CreateArtifact` in all 5 attempts. The same job passed on macOS in run 41090163655, and on
  Windows and Linux in the failing run itself. The upload step cannot run locally. The package
  build and check it depends on passed on all three platforms.
- No code change. Nothing in the repository causes or can prevent a GitHub service timeout, and
  the workflow file (`.github/workflows/ci.yml`) is off-limits to lanes, so adding retries there
  is not a lane's job. The tree is unchanged, so dotnet build and the fast tests were not rerun:
  CI built and tested this tree green on Windows, Linux and macOS in this same run.
- The CI criterion is met by the next push: the job only needs GitHub's artifact service to be
  back. The same run's separate `Quality gates (windows-latest)` failure is not part of this
  task. The CI watch files it as its own task.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: c011fb87.
- 2026-10-30: Doing -> Done. Transient GitHub artifact-service timeout (CreateArtifact); packages built and checked, no code fault
