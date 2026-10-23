---
id: BL-3155
title: Stop the shift-end merge following a newer head the next shift pushed after a hand-over
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/ShiftEnd.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3155 — Stop the shift-end merge following a newer head the next shift pushed after a hand-over

## Goal

After a hand-over has started the next shift during the merge's CI wait, the shift-end merge no longer adopts a newer branch head that holds the next shift's commits.

## Context

Found by the shift-end review of 2026-10-22 (ADR-0710), reviewer code-reviewer, in `DarkFactory/ShiftEnd.ps1:34-40` (`Wait-MergeCiRun`) together with `DarkFactory/ShiftCoordinator.ps1:466-476` (BL-3142).

When a newer push cancels the waiting CI run (ADR-0693), `Wait-MergeCiRun` follows `origin/$Branch` to the new head. Before BL-3142, only `[skip ci]` board commits reached the branch during the wait. Now `-Beside` starts the next shift during the wait, on the same `-ShiftBranch`, and its lanes push code there. Then:

- This shift's waiting run is cancelled by a next-shift lane's push.
- The merge follows the new head, which carries the next shift's partial work.
- That work is merged to `master` as this shift's merge, and `$review` covers commits that the next shift's own review will cover again.
- If lanes keep pushing, the wait can instead hit `$MergeCiWaitMinutes` and leave this shift's work unmerged.

## Acceptance criteria

- [ ] Once a hand-over has happened, `Wait-MergeCiRun` follows a newer head only if every commit since the original head is a `[skip ci]` board commit. Otherwise it returns a "not merged: ..." line naming the hand-over.
- [ ] Without a hand-over, a newer head is followed as before (ADR-0693).
- [ ] A test in the factory's test blocks covers both cases.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 3f408fbf e80f142e 20efafd0.
