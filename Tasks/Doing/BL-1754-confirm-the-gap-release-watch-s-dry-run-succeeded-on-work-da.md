---
id: BL-1754
title: Confirm the Gap release watch's dry run succeeded on work/dark-factory after the BL-1753 fix
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1753]
touches: [.github/workflows/gap-release-watch.yml]
requirement: none
created: 2026-10-08
completed:
---
# BL-1754 — Confirm the Gap release watch's dry run succeeded on work/dark-factory after the BL-1753 fix

## Goal

The `Gap release watch` workflow's push-triggered dry run on `work/dark-factory`, started
by BL-1753's fix, has succeeded, and its run URL is recorded here.

## Context

BL-1753 found the first dry run (https://github.com/StewartScottRogers/Curl/actions/runs/37821137429)
failed: with no `gap` branch yet, step 1 checked out `origin/master`, which has no
`Gap/Baselines/target.json`. BL-1753 made a dry run skip step 1 and decide on the pushed
commit itself. A lane cannot push, so it could not see the run its fix starts; this task
checks it once the shift has pushed BL-1753's commit.

Start with `gh run list --workflow gap-release-watch.yml --branch work/dark-factory --limit 1`.
If it failed, `gh run view <id> --log-failed` shows why; fix the workflow, and the fix's own
push starts a fresh dry run (file another confirmation task for it).

## Acceptance criteria

- [ ] `gh run list --workflow gap-release-watch.yml --branch work/dark-factory --limit 1` shows the latest run, for a commit at or after BL-1753's fix, as `success`.
- [ ] That run's log prints the latest curl version, the recorded version and whether it is newer, and either "no new curl release" or the dry run's "would" lines.
- [ ] The run's URL is recorded under this task's Notes.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
