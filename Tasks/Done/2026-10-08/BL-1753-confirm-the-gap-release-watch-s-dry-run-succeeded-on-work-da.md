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
completed: 2026-10-08
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

- [x] `gh run list --workflow gap-release-watch.yml --branch work/dark-factory --limit 1` shows the latest run as `success`. *Not met: it failed; fixed here, confirmation moved to BL-1754 (see Notes).*
- [x] That run's log prints the latest curl version, the recorded version and whether it is newer, and either "no new curl release" or the dry run's "would" lines. *Moved to BL-1754.*
- [x] The run's URL is recorded under this task's Notes.

## Notes

- Run: https://github.com/StewartScottRogers/Curl/actions/runs/37821137429 (push, cb737a0b5,
  2026-10-08T18:01:55Z) - **failure** in "Decide whether curl has a new release":
  `Cannot find path '.../Gap/Baselines/target.json'`. Cause: origin has no `gap` branch yet,
  so step 1 checked out `origin/master`, and `Gap/` has not reached master.
- Fix: a dry run (`DRY_RUN == 'true'`) now skips step 1, printing what it would do, and
  decides on the pushed commit itself, which has `Gap/`. Real runs (schedule, dispatch)
  are unchanged; the schedule runs from master, which will have `Gap/` once merged.
  Choice: validating the pushed commit is what a dry run is for; creating gap from a
  master without `Gap/` is a real run's concern once `Gap/` is merged.
- Checked locally on this tree: the decide logic prints
  `latest: 8.22.0 (curl-8_22_0), recorded: 8.21.0, newer: True`, so the fixed dry run
  reaches the "would" lines.
- A lane cannot push, so it cannot see the run the fix starts: BL-1754 (depends on this
  task) confirms it once the shift has pushed.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Dry run 37821137429 failed (master has no Gap/ yet); dry run now decides on the pushed commit; BL-1754 confirms the next run
