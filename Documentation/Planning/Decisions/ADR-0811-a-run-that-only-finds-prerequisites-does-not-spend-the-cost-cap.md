# ADR-0811 — A run that only finds prerequisites does not spend the cost cap

- **Status:** Accepted
- **Date:** 2026-10-24
- **Amends:** AF-0095's rule in `DarkFactory/TaskBudget.ps1` that a task's runs of the last day come off its next run's cap (ADR-0760 keeps the $2 floor)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-3364,
BL-3451, BL-3452 and BL-3453.

## Context

AF-0387, AF-0404, AF-0405 and AF-0406 found four tasks (BL-3204, BL-3412, BL-3315,
BL-3234) whose first claim found the task waiting on other work and sent it back to Backlog
having written no code, at $0.73 to $0.86 a run (BL-3315's and BL-3234's twice). That cost
came off the cap of the claim that did the work once the prerequisites were Done: BL-3412's
second claim stopped at $2.19 of the $2.27 left of $3.00 and BL-3204's ran out with $2.60 of
its $3.72 spent, each with the work stashed, and BL-3315 and BL-3234 were unclaimable over the cap. All four were split
into tasks that landed the stashed work: about $2 to $3 more each, and two more claims.

A run that finds only prerequisites spends the cap on reading, not on the task's work. The
daily rule exists so a task requeued again and again cannot spend without limit (AF-0096,
AF-0326); a prerequisite requeue cannot repeat that way, because the board claims a task
only once every ID in its `depends-on` is Done, so the next claim waits for the work it named.

## Decision

After every task run, `Register-PrerequisiteRequeueRun` (`DarkFactory/TaskBudget.ps1`,
called from `DarkFactory/ShiftRunner.ps1` before `Save-StrayChanges`) waives the run when
the task is in Backlog, the worktree has nothing to stash, no stash of the task waits, and
its `depends-on` names an ID not Done. Every such ID was added by this run, since all of
them were Done at the claim. It writes `<ID>-<stamp>[-L<n>].prerequisite-requeue.txt`
beside the run's log, with the reason, and traces `waive`. `Get-TaskRunSpends` skips a run
log (and that claim's `-resumed` log) with that marker, so `Get-TaskSpentUsd`,
`Test-TaskCapSpent` and `Get-CapResetTime` do not count it. The recent-run median
(`Get-RecentRunCosts`) still counts it.

The lane prompt tells a run that a requeue for other work with nothing to stash is waived,
that one which edited first is counted, that a task found holding a file it needs goes into
`depends-on` as well (BL-3204's run left it empty), and that tasks filed together name in
`depends-on` the ones they extend (BL-3412 was ready beside BL-3410).

## Consequences

- The claim after a prerequisite requeue gets a whole cap, so it can finish rather than
  stash and be split.
- A requeue that leaves `depends-on` without an open ID, or that leaves code, still counts.
  So does a requeue for overlapping `touches` unless the run puts the holder in `depends-on`.
- It does not stop a second prerequisite requeue (a first run that missed a prerequisite);
  the prompt's one-pass rule (AF-0147, AF-0242) is still what does that, and now a second
  discovery costs the reading only once against the cap.
- The marker is a new file kind in the logs folder; the audit's `Measure-FactoryProcess.ps1`
  reads only `DarkFactory-*-L*.log` and `BL-*.jsonl`, so it does not see it.
