---
id: BL-2013
title: Make the gap and audit task filers allocate IDs from the latest board and dedupe before pushing
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Gap/Tools, Audit/Tools]
lane: no
requirement: none
created: 2026-10-10
completed: 2026-10-11
---
# BL-2013 — Make the gap and audit task filers allocate IDs from the latest board and dedupe before pushing

## Goal

When the audit office and the gap office file tasks at about the same time, no two live tasks share an ID.

## Context

On 2026-10-10 the Audit tab released AF-0115 and AF-0141..AF-0152 as BL-1959..BL-1971 at 07:08. The gap run started at 06:57 and filed its own BL-1959..BL-1971 at 07:58, from the board as it stood when it started. `task-board.ps1 move` refuses an ID that names two live tasks, so all 9 lanes of the 08:00 shift claimed nothing for 80 minutes ("claim kept losing races"). It was fixed by hand: `dedupe -Since 7f0e9695b` renumbered the gap's tasks to BL-1997..BL-2009 (702cd24ed), and PR #104 repointed GF-0001..GF-0016. Start from the gap run's task filing in Gap/Tools and from Audit/Tools/New-TasksFromAcceptedFindings.ps1 and the Audit tab's release path.

## Acceptance criteria

- [x] Each filer fetches origin/work/dark-factory right before it allocates IDs. If its push is refused, it re-fetches, runs `task-board.ps1 dedupe -Since <the ref it started from>`, repoints its findings' `task:`/`tasks:` fields to the new IDs and pushes again.
- [x] A test or a `-WhatIf` dry run shows two filers started from the same board end up with distinct IDs, and each finding's task links name its own tasks.

## Notes

- 2026-10-11 (interactive). **Gap filer:** `Gap/RunGapAnalysis.ps1` step 10, on the `gap` branch (00e050676, PR #113). It runs `git fetch origin work/dark-factory` right before it makes the board worktree and calls `New-TasksFromGaps.ps1`. Before every push, not only after a refused one, `Sync-GapBoard` fetches, rebases onto `origin/work/dark-factory`, runs `task-board.ps1 dedupe -Since <the commit rebased onto>` and amends the commit. Every renumbering, joined across tries, is then rewritten in the findings by `Update-GapFindingTaskIds` before the "gap: task links" commit.
  - `-Since` deviates from the criterion's wording on purpose. It is the commit rebased onto, not the ref the filer started from. Measured against the start ref, both filers' tasks would be "new" and dedupe would keep the first by path order, not the one already pushed.
  - Every try dedupes, because on 2026-10-10 no push was refused: `pull --rebase` replayed cleanly over files with different names, and the duplicates went straight in.
- **Audit filer:** the Audit tab's release (`.github/workflows/release-approved-findings.yml`) already fetches, rebases, dedupes against the commit rebased onto, and rewrites renumbered IDs in the findings. It did so on 2026-10-11 for AF-0150 (ea855e20a). The manual path in `Audit/Triage.md` step 3 now says the same (3051e18c4, on PR #112).
- **Test:** `Gap\RunGapAnalysis.ps1 -SelfTest` passes 23 checks, 6 of them new. `Test-GapTwoFilers` builds a bare origin with a one-task board and two clones; each files a BL-002, the first pushes, and the second goes through `Sync-GapBoard` and pushes. origin ends with `BL-002-audit-task.md` and `BL-003-gap-task.md`, and a finding naming the second's BL-002 now names BL-003. The real `task-board.ps1` runs there with real git.
- Found on the way: `dedupe` leaves git `cat-file`'s exit code 1 in `$LASTEXITCODE`, so `exit $LASTEXITCODE` after it reports failure. The gap's call exits 0 unless the script throws.

## Log

- 2026-10-10: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. Gap runs number tasks from a fresh board and dedupe against the commit rebased onto before each push, rewriting findings (PR #113); the audit release already did; self-test proves two filers end distinct
