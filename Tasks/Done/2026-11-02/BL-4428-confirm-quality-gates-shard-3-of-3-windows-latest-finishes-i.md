---
id: BL-4428
title: Confirm Quality gates shard 3 of 3 (windows-latest) finishes inside its timeout after the hang fix (split from BL-4421)
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-4427]
touches: [Tasks]
requirement: none
created: 2026-10-31
completed: 2026-11-02
---
# BL-4428 — Confirm Quality gates shard 3 of 3 (windows-latest) finishes inside its timeout after the hang fix (split from BL-4421)

## Goal

`Quality gates shard 3 of 3 (windows-latest)` is shown to finish inside its `timeout-minutes` on a CI run that includes the hang fix.

## Context

Split from BL-4421 (its criterion 2). Depends on BL-4427, which names and removes the cause. Check with `gh run list --workflow CI` and `gh run view <id>`.

## Acceptance criteria

- [x] `Quality gates shard 3 of 3 (windows-latest)` finished successfully inside its `timeout-minutes` in the `CI` workflow for a commit that contains BL-4427's fix, with the run ID named under Notes.

## Notes

- Split from BL-4421.
- 2026-11-02 (lane 2): Confirmed. CI run 41467980936 (work/dark-factory, head e3af7deb5, 2026-11-02T15:04Z) contains BL-4427's fix (BL-4442 59686eeba and BL-4444 62a300628 are ancestors of its head). Its job 125357130218 `Quality gates shard 3 of 3 (windows-latest)` concluded success, running 15:10:24Z to 15:20:02Z (9 min 38 s); a job that hits `timeout-minutes` is cancelled, not successful, so it finished inside the timeout. No code changed.

## Log

- 2026-10-31: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 348e26be.
- 2026-11-02: Doing -> Done. Shard 3 passed in 9m38s on CI run 41467980936, which contains the BL-4442/BL-4444 hang fix
