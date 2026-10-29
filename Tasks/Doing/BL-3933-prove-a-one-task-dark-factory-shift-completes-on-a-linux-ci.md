---
id: BL-3933
title: Prove a one-task dark factory shift completes on a Linux CI runner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3923, BL-3925, BL-3926, BL-3932]
touches: [.github/workflows/dark-factory-unix-shift.yml]
requirement: none
created: 2026-10-27
completed:
---
# BL-3933 — Prove a one-task dark factory shift completes on a Linux CI runner

## Goal

A GitHub Actions job on `ubuntu-latest` runs a real one-lane dark factory shift end to end on a
scratch copy of the board, with a stand-in for the `claude` command, and shows the task claimed,
run, integrated and moved to `Done`.

## Context

Part of BL-3934, and the last check before Stewart tries a remote Unix host. The self-tests
(BL-3923) prove the pieces; this proves the loop. No Claude tokens in CI, so the job puts a small
stand-in `claude` executable first on PATH that answers the way a headless run does (read
`DarkFactory/TaskRun.ps1` for what the factory parses) and makes a trivial change for a scratch
task. The shift must not push to the real remote: point it at a local bare repository the job
creates. If `RunDarkFactory.ps1` lacks a way to do either, add the smallest parameter that allows
it, in its own file under `DarkFactory/` per BL-2988's layout, and list it in the help. Uses the
host setup from BL-3932 where it fits.

## Acceptance criteria

- [ ] `.github/workflows/dark-factory-unix-shift.yml` runs on pushes to `work/dark-factory` and on demand, on `ubuntu-latest`, and finishes a one-task shift against a local bare remote with a stand-in `claude`.
- [ ] The job fails unless the scratch task ends in `Done` with the stand-in's change committed and pushed to the local bare remote.
- [ ] A link to a green run of the job for the commit that adds it is in this task's Log.
- [ ] Any new `RunDarkFactory.ps1` parameter is in its help and covered by a `-Test*` self-test, and `dotnet build` plus the fast tests are green.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 66a24898 371f6b50 ed5482f5 daf71697.
