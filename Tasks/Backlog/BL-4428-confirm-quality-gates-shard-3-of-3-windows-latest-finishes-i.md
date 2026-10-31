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
completed:
---
# BL-4428 — Confirm Quality gates shard 3 of 3 (windows-latest) finishes inside its timeout after the hang fix (split from BL-4421)

## Goal

`Quality gates shard 3 of 3 (windows-latest)` is shown to finish inside its `timeout-minutes` on a CI run that includes the hang fix.

## Context

Split from BL-4421 (its criterion 2). Depends on BL-4427, which names and removes the cause. Check with `gh run list --workflow CI` and `gh run view <id>`.

## Acceptance criteria

- [ ] `Quality gates shard 3 of 3 (windows-latest)` finished successfully inside its `timeout-minutes` in the `CI` workflow for a commit that contains BL-4427's fix, with the run ID named under Notes.

## Notes

- Split from BL-4421.

## Log

- 2026-10-31: Created.
