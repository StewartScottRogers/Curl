---
id: BL-1741
title: Turn a gap analysis run's measurements into findings and a scorecard and Curl tasks in Gap/RunGapAnalysis.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1740, BL-1731, BL-1732, BL-1733, BL-1734, BL-1737, BL-1738, BL-1739]
touches: [Gap/RunGapAnalysis.ps1]
requirement: none
created: 2026-10-08
completed:
---
# BL-1741 — Turn a gap analysis run's measurements into findings and a scorecard and Curl tasks in Gap/RunGapAnalysis.ps1

## Goal

After BL-1740's measurements, `Gap/RunGapAnalysis.ps1` runs each area's analyst. It writes
findings, the scorecard, the history entry and the dashboard data on the `gap` branch, opens
the `gap` to `master` pull request, and files the Curl tasks on the factory's board. A full
run then needs no further hand work.

## Context

This is ADR-0433 decisions 3, 4, 5 and 7. It builds on BL-1740's script and calls:

- `Write-GapFindings.ps1` (BL-1731), `Write-GapScorecard.ps1` (BL-1732),
  `Export-GapDashboardData.ps1` (BL-1733) and `New-TasksFromGaps.ps1` (BL-1734);
- the analysts `gap-options`, `gap-environment` (BL-1737), `gap-protocols`,
  `gap-features`, `gap-writeout`, `gap-exitcodes` (BL-1738) and `gap-behaviour`
  (BL-1739).

**Where things are written.** ADR-0433 decision 7 sets the destinations:

- findings, scorecards, history and inventories go to the `gap` branch, never
  `work/dark-factory`;
- tasks go to the factory's board on `work/dark-factory`.

**Steps added before BL-1740's step 2.**

- Use the worktree that has `gap` checked out. Otherwise create `<repo>.gap\gap-branch` on
  it, from `origin/gap`, or else from `origin/master`. Merge `origin/master` into it, never
  rebase. A rebase of the audit branch replayed hundreds of commits on 2026-10-04.
- Run the measurement tools from that worktree's `Gap/Tools/`.

**Steps added after BL-1740's step 6.**

7. For each measured area, run its analyst headless, the way the dark factory runs agents:
   `claude -p --agent gap-<area> --model <the agent's model> --dangerously-skip-permissions
   --output-format stream-json --verbose`, with the prompt on standard input. Search
   `RunDarkFactory.ps1` for `--output-format stream-json` for the exact invocation and how
   the result event gives the cost. The prompt names:
   - the run folder, the measurement file and the tree;
   - the release folder;
   - the area's existing findings: id, key, status and items, read from the gap branch's
     `Gap/Findings/`;
   - the run stamp.

   Save the reply to `<stamp>\reports\gap-<area>.md` and log the cost. When the tree is left
   changed afterwards (`git -C <tree> status --porcelain` is not empty), log the analyst as
   having written to the measured tree, reset the tree, and drop its report, so its gaps
   fall to the `ungrouped` finding (BL-1731 rule 2).
8. In the gap worktree:
   - `Write-GapFindings.ps1 -RunDirectory <stamp> -Stamp <stamp>`, then
     `Write-GapScorecard.ps1`;
   - copy the measured inventories (`Gap/Upstream/<version>/`) when they changed;
   - run `Export-GapDashboardData.ps1 -OutFile <stamp>\data.json`.
9. Commit in the gap worktree as `gap: scorecard <stamp>` and push `origin gap`. Then open
   a pull request `gap` to `master` with `gh pr create` unless one is open. The body holds
   the scorecard's Scores table and ends with the attribution line the repository's
   instructions require. Never merge it. An interactive session merges it once CI is
   green, as for audit pull requests.
10. Unless `-NoTasks` is given, file the tasks:
    - create a temporary worktree of `origin/work/dark-factory`;
    - run `New-TasksFromGaps.ps1 -BoardRoot <that worktree>`;
    - commit as `chore(tasks): file gap tasks from <stamp>`, then `git pull --rebase` and
      push `work/dark-factory`, retrying three times;
    - remove the worktree.

    Then commit the findings' new `task` fields on the gap branch and push again.
11. Remove the measured tree worktree. Keep `<stamp>` for reading.

Add these switches: `-NoTasks` (skip step 10), `-NoPullRequest` (skip the `gh pr create`
call), and `-NoCommit` (write everything into the gap worktree but skip steps 9 and 10).
`-DryRun` covers the new steps too.

## Acceptance criteria

- [ ] `Gap\RunGapAnalysis.cmd -DryRun` prints steps 1 to 11, each with its exact command, and changes nothing.
- [ ] `-SelfTest` adds `PASS` checks for: the gap worktree is chosen or created from `origin/gap`, else `origin/master`; an analyst that changes the tree is logged and its report dropped; `-NoTasks` skips step 10; `-NoPullRequest` skips the `gh pr create` call. All of these run against faked `git`, `gh` and `claude` commands, so the self-test needs no network.
- [ ] A real run `Gap\RunGapAnalysis.cmd -Areas exitcodes -NoTasks -NoCommit -AlongsideShift` writes `reports\gap-exitcodes.md`, at least one finding or a scorecard showing none, `history.json` with one new entry, and `data.json`, all in the gap worktree. It commits nothing, pushes nothing and opens no pull request. Afterwards the gap worktree's changes are discarded with `git -C <gap worktree> checkout -- . ; git -C <gap worktree> clean -fd Gap`, and the outcome is recorded in this task's Notes.
- [ ] The header help documents every step and switch. The script is ASCII only and runs under Windows PowerShell 5.1.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
