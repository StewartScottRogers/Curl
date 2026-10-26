---
id: BL-3834
title: Renumber a lane's new ADR when another lane pushed the same number first
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Integrate.ps1, DarkFactory/AdrIds.ps1, DarkFactory/LaneRules.md]
requirement: none
created: 2026-10-26
completed:
---
# BL-3834 — Renumber a lane's new ADR when another lane pushed the same number first

## Goal

When a lane integrates, any ADR it adds whose number another commit on the shared branch already uses is renumbered to the next free number before the push, so no two ADRs share a number.

## Context

Shift 20261026-063535 (9 lanes, gap report tasks): by 08:37 Arizona on 2026-10-26, `Documentation/Planning/Decisions` on `work/dark-factory` held six ADR-0815s, seven ADR-0816s and four ADR-0817s, and 14 numbers in all are shared. Each lane picks the next ADR number from its own worktree, as task IDs once did; tasks are fixed by `task-board.ps1 dedupe -Since` after the rebase (`DarkFactory/Claim.ps1` around line 151, `DarkFactory/TaskIds.ps1`), but nothing does the same for ADRs. BL-326 renumbered a duplicate ADR-0040 by hand.

Do the same as task dedupe: after the rebase in `DarkFactory/Integrate.ps1`, ADR files present at the rebase base keep their numbers; an ADR the lane's commits add that shares a number with another file gets the next free number, its file name and `# ADR-####` heading change, and every reference to the old number in files the lane's commits changed is rewritten. Put the logic in its own `DarkFactory/AdrIds.ps1` (loaded by the loader in its fixed order, BL-2988) with a self-test, as `TaskIds.ps1` does. Add the rule to `DarkFactory/LaneRules.md` if lanes need to know it.

## Acceptance criteria

- [ ] A scratch-repository test in `AdrIds.ps1`'s self-test: two branches each add `ADR-0900-*.md` and cite it from a task file; after the second is rebased and integrated, its ADR is `ADR-0901-*.md`, its heading and its citing task say ADR-0901, and the first branch's ADR-0900 is unchanged.
- [ ] An ADR present at the rebase base is never renumbered, even if it already shares its number (that cleanup is BL-3835).
- [ ] A failure in the renumbering stops the push and says why, as a failed task dedupe does (BL-3491).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.