# ADR-0759 — A task over its cost cap is split by one bounded run, once a day

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3091
- Decided by Claude under Stewart's delegation.

## Context

The 2026-10-21 20:04 shift ended with BL-2970, BL-2925, BL-2834, BL-2881 and BL-2966 all over
their cost cap. Each sat in Blocked or was passed over until an interactive session split them
(commit d1aa8a5a3): it filed BL-3011..BL-3023, stored each original's stash again under the new
IDs with `git stash store`, deferred the originals and repointed their dependents. Until then
every task depending on them was stalled.

## Decision

- `Invoke-TaskSplit` (`DarkFactory/TaskStash.ps1`) does that split with one headless run built
  like a task run: `claude -p --model sonnet`, `--max-budget-usd 1`, `--max-turns 30`, a
  20-minute limit, and `$SplitForbidden`, which denies every git command a lane may not run and
  every `git stash` subcommand except `list` and `store`. Its prompt is `$SplitPrompt`
  (`DarkFactory/Prompts.ps1`).
- It runs when a claim passes over a task for its cost cap (`Invoke-Claim`, after the claim
  lock is released, so a split run never holds every lane's claims) and when a run stops at its
  cost cap or a fresh claim finds the cap spent (`ShiftRunner.ps1`).
- At most once per task per day: the first offer writes `<log>/splits/<id>-<yyyyMMdd>.txt`;
  later offers that day return `skipped`, and the shift blocks the task as it did before.
- The split counts only when the run exits 0, a new task file appeared and the original is in
  Deferred. The shift, not the run, commits and pushes the Tasks change, inside `Enter-Lock`.
  Otherwise it restores `Tasks/`, moves the task to Blocked with a reason naming the failure,
  and commits that under the lock.
- From a claim (checkout just synced) a refused push pulls with rebase and pushes again; after a
  task's run the lane may hold its own commits, which only integration may push, so there the
  split commit is left for the shift's integration; neither it nor a failed split's block commit is pushed there (BL-3100).

## Why

- Sonnet and $1 / 30 turns: splitting reads two files and runs the board script a few times;
  it writes no code, so it needs neither Opus nor a task-sized budget, and a cap well below the
  task's keeps a failed split cheap. The criteria allow up to $2.
- Once a day matches the cost cap's one-day window: if the split fails, the task is blocked for
  Stewart rather than retried at every claim, and the next day's capped sighting may try again.
- The shift committing keeps the run off the remote, like every lane run (`$LaneForbidden`).
