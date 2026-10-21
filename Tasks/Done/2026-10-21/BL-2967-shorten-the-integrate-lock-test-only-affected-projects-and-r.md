---
id: BL-2967
title: Shorten the integrate lock: test only affected projects and resolve rebase conflicts outside the lock
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2967 — Shorten the integrate lock: test only affected projects and resolve rebase conflicts outside the lock

## Goal

A lane holds the integrate lock for about a minute instead of 3.3, so a shift can finish about 40 tasks
an hour instead of 18.

## Context

Stewart, 2026-10-21, approved: "I accept your recommendation to shortening the integration step."
Between 11:07 and 11:53 Arizona on shift 20261021-064209, 14 pushes went through the lock at about
3.3 minutes each, and 4 of the 9 lanes were waiting for it. Under the lock, `Test-Green` builds the
whole solution and runs all ~40,000 fast tests, and a rebase conflict runs a Claude resolve session
(6 to 8 minutes each for L7, L5, L2 and L8 in that window) while every other lane waits.

## Acceptance criteria

- [x] Under the lock, a finished task's verify runs only the `*.UnitTests` projects that reference,
      directly or through other projects, a project its commits change; a change to
      `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `global.json`,
      `Curl.slnx`, `CodeMetricsConfig.txt` or `.editorconfig` runs all of them; a change to no project
      runs none. The build stays a whole-solution build.
- [x] A rebase conflict is resolved with the lock released; the lane then takes the lock again and
      rebases once more before verifying and pushing.
- [x] A self-test switch proves the affected-project selection and the release-and-retake on a
      throwaway repository, and passes; `-TestAutoLanes` and `-TestCiWatch` still pass.
- [x] The script header and `CLAUDE.md` say what a lane tests before pushing, and that CI still
      runs every test on all three platforms.

## Notes

Its own ADR records the decision.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Interactive session; Stewart approved 2026-10-21. Criteria at claim: fbecace0.
- 2026-10-21: Doing -> Backlog. Criteria were written after the claim; reclaiming with them.
- 2026-10-21: Backlog -> Doing. Reclaimed with the written criteria. Criteria at claim: 9548ec98 af551e26 ce6af7a5 96bf6200.
- 2026-10-21: Doing -> Done. Lanes test only affected projects under the integrate lock and resolve rebase conflicts with it released; ADR-0743; self-tests 167/167.
