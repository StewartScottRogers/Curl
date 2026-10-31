# ADR-1155 — A task is claimed at most twice: splits defer, and a second requeue splits

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4183 to BL-4191 and BL-4227 (AF-0465 to AF-0474)
- Decided by Claude under Stewart's delegation.

## Context

The process auditor found tasks claimed three or more times and requeued twice or more
(AF-0465 to AF-0474). Over the live trace logs from 2026-10-29 to 2026-10-30 08:12 there were
61 `REQUEUE` lines: 25 "Split for size", 22 waits on or needs another task, 5 "out of time"
hand-backs with the code written and only the quality run, fast tests and commit left, 3 at the
80-turn cap, 3 at the cost cap, 1 park and 2 partial hand-backs. BL-4124, BL-4132 and BL-4137 were claimed three
times; BL-4131 and BL-4170 requeued twice.

Three mechanisms made the bounce structural rather than bad luck:

1. The task prompts told a run that split a task for size to "split it ... and requeue it": the
   parent went back to Backlog with its own subtasks in `depends-on` and the part of the work
   the run had not filed (for an extension, the client flow and its hook) left in it. Every
   split parent was therefore claimed a second time, and when that claim ran short (the
   deadline for BL-4131, BL-4132 and BL-4137, a park for BL-4124) a third.
2. The deadline rule told a run that could not finish to file what was left as a task, add it
   to the task's `depends-on` and requeue: by construction a second requeue and a third claim.
3. A run stopped at its turn cap went to Backlog "to be split in the first turns of the next
   claim": a second claim whose only job was a split (BL-4112, BL-4328, BL-4341), while a run
   stopped at its cost cap already got the bounded split run of ADR-0759 at once.

## Decision

- A split for size never sends the task back to Backlog. The run files every part of the
  work, the rest of the task included as the last part depending on the others, repoints every
  dependent's `depends-on` to the new IDs, and moves the task to Deferred, as `$SplitPrompt`
  already does for the cost cap. It splits before the first edit.
- The deadline rule hands the task back with its work for the shift to stash, without filing
  the rest as a task the task then waits on.
- A run stopped at its turn cap gets the bounded split run at once (`Invoke-TaskSplit -Cause
  'too big for one run'`), like a run stopped at its cost cap.
- The backstop: when a run leaves its task in Backlog and the task's Log already shows an
  earlier `Doing -> Backlog` move (a requeue or a park; a claim released unstarted at the
  shift's end does not count, `Get-TaskRequeueCount`), `ShiftRunner.ps1` splits it with the
  same bounded run (`-Cause 'sent back to Backlog twice'`), which carries any stash to the new
  tasks and defers the original. A split skipped (offered once already that day) or failed
  blocks it with a `Stewart:` reason to triage, rather than offering it a third claim.
- `Invoke-TaskSplit` takes `-Cause` (default `over its cost cap`), used in its prompt, trace and
  commit message, so a split for another cause does not claim the cost cap was spent.

## Consequences

- A task is claimed at most twice and requeued at most once; the measurement of AF-0465 to
  AF-0474 (`Audit/Tools/Measure-FactoryProcess.ps1`) should show no task with 3 claims or 2
  requeues in logs written after this lands.
- Waits on a missing prerequisite stay a single requeue: the board already holds such a task
  until its new `depends-on` IDs are Done, so it is not offered again before then.
- A deferred parent never reaches Done; its subtasks carry its acceptance criteria. Dependents
  wait on the subtasks instead.
- A task blocked by the backstop needs Stewart or an interactive session; that is the rare
  case where the split run itself failed.
- `-TestTaskStash` proves the count, the cause, the turn-cap split and the prompt wording.
