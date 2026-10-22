---
id: BL-1841
title: Stop the gap-options analyst writing into the measured tree
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/agents/gap-options.md, Gap/Instructions]
lane: no
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1841 — Stop the gap-options analyst writing into the measured tree

## Goal

The gap-options analyst reports without changing the measured tree, so the options area gets findings.

## Context

- First gap run 2026-10-08_1640: `Analyst gap-options wrote to the measured tree; tree reset and its report dropped`. The options area (567 of 573, 6 gaps) filed no findings. Read the analyst's transcript in the run folder to see what it wrote and why.
- Tighten `.claude/agents/gap-options.md` or its instructions, or give it the scratch folder the rules allow. The other six analysts reported cleanly.
- Interactive only (`.claude/agents/gap-*` is an audit path).

## Acceptance criteria

- [x] Notes name what the analyst wrote and the cause.
- [x] The next gap run's options analyst returns a report and the run files the options gaps as findings.
- [x] Merged to master through a green audit pull request.

## Notes

- 2026-10-08: Merged in PR #82. The options analyst wrote nothing (its transcript holds only reads). `Measure-ExitCodeGap.ps1` rewrote `Gap/Upstream/8.21.0/exitcodes.json` inside the measured tree before any analyst ran, and the after-analyst check blamed the first analyst. Exit code, environment and version measurers now take `-InventoryDirectory`, defaulting to their own repository's `Gap/Upstream`; `RunGapAnalysis.ps1` checks and resets the tree before the first analyst and names the files. Rerun of the options analyst against the real measurement returned its report with the tree clean (0.23 USD).

- 2026-10-08: Merged in PR #82. The options analyst wrote nothing (its transcript holds only reads). `Measure-ExitCodeGap.ps1` rewrote `Gap/Upstream/8.21.0/exitcodes.json` inside the measured tree before any analyst ran, and the after-analyst check blamed the first analyst. Exit code, environment and version measurers now take `-InventoryDirectory`, defaulting to their own repository's `Gap/Upstream`; `RunGapAnalysis.ps1` checks and resets the tree before the first analyst and names the files. Rerun of the options analyst against the real measurement returned its report with the tree clean (0.23 USD).

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Blocked. Being worked interactively on branch audit-bl-1841; parked so Doing holds nothing orphaned between shifts
- 2026-10-08: Blocked -> Doing.
- 2026-10-08: Doing -> Done. Merged in PR #82 with CI green on all three platforms
