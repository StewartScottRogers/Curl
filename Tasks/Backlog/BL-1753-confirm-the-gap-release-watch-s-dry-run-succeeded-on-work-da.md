---
id: BL-1753
title: Confirm the Gap release watch's dry run succeeded on work/dark-factory
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1736]
touches: [.github/workflows/gap-release-watch.yml]
requirement: none
created: 2026-10-08
completed:
---
# BL-1753 — Confirm the Gap release watch's dry run succeeded on work/dark-factory

## Goal

The `Gap release watch` workflow's push-triggered dry run on `work/dark-factory` has
succeeded, and its run URL is recorded here.

## Context

BL-1736 wrote `.github/workflows/gap-release-watch.yml`, whose `push` trigger (to
`work/dark-factory`, only when that file changes) runs steps 1 to 3 and prints what steps 4
to 6 would do, with no commit and no push. A dark factory lane may not push, so BL-1736
could not see that run; this task checks it once the shift has pushed BL-1736's commit.

Start with `gh run list --workflow gap-release-watch.yml --branch work/dark-factory --limit 1`.
If it failed, `gh run view <id> --log-failed` shows why; fix the workflow, and the fix's own
push starts a fresh dry run.

## Acceptance criteria

- [ ] `gh run list --workflow gap-release-watch.yml --branch work/dark-factory --limit 1` shows the latest run as `success`.
- [ ] That run's log prints the latest curl version, the recorded version and whether it is newer, and either "no new curl release" or the dry run's "would" lines.
- [ ] The run's URL is recorded under this task's Notes.

## Notes

## Log

- 2026-10-08: Created.
