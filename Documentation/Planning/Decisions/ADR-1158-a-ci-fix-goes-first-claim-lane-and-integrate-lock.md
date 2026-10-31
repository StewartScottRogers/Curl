# ADR-1158 — A CI fix goes first: claimed before other work, given a lane, and integrated ahead

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4228, BL-4229, BL-4230, BL-4231, BL-4232, BL-4233, BL-4234 (AF-0475 to AF-0481)
- Decided by Claude under Stewart's delegation.
- Builds on ADR-0811 (the CI watch reads running runs and cancels a run that lacks a CI fix).

## Context

The process auditor found `work/dark-factory` red for more than 30 minutes again and again.
Measured over the live logs since 2026-10-29 (`Measure-FactoryProcess.ps1`, `.ciRedSpells`),
three spells were still over 30 minutes. The logs show where the time went (times UTC):

- Run 40885502110, 237 minutes (14:07 to 18:04 on 2026-10-29). Its Quality gates failure was
  filed at 14:19 as BL-4083. A lane claimed it at 14:21. The run itself sent it back to Backlog
  at 14:23, "waiting on BL-4082", which held `Curl.Engine.UnitLibrary`. BL-4082 landed at 14:24.
  After that, every lane of that shift skipped BL-4083 because it was on the shift's requeued
  list (`Get-ShiftRequeued`). From 14:49 to 15:20 lanes idled with "No task is ready" beside it.
  The next shift started at 15:50. `task-board.ps1 next` ranked BL-4083 sixth: High feature
  tasks with more tasks waiting on them (BL-4093 had about 70) came first, and those tasks
  overlapped BL-4083's files. It was claimed at 17:46, and landed at 18:02.
- Run 40984137987, 139 minutes (01:30 to 03:49 on 2026-10-30). The run failed during a shift
  change, and the new coordinator filed BL-4299 at 01:50. All 13 adopted lanes were busy, so
  BL-4299 was not claimed until 02:31 (41 minutes). The fix ran 10 minutes and landed at 02:48.
  The next run then failed on a new Quality gates failure, BL-4308. A lane claimed BL-4308 at
  once and fixed it by 03:06. Its integration then waited for the integrate lock until 03:33
  (27 minutes), behind eight ordinary integrations. The green run took 15 minutes.
- Run 40832703845, 49 minutes (07:12 to 08:01 on 2026-10-29). This was the tail of AF-0481's
  spell, cut by the measurement's start. BL-4052 was filed at 07:10 and waited 21 minutes for
  one of 8 busy lanes. Its fix took 5 minutes, and the green run took 24 minutes.

The measurement ignores cancelled runs. That was not the cause: every spell ended with the
first finished green run after the fix landed.

## Decision

A CI fix is a task titled "Fix CI failure ...", "Fix CI build ..." or "Fix flaky CI test ..."
(`Test-CiFixTitle`, which now includes "Fix CI build"). It goes before all other work:

1. **Claimed first.** Before a claim asks `next`, it takes the oldest CI fix in Backlog that
   meets all of these:
   - its depends-on are all Done;
   - it is not interactive only and not over its cost cap;
   - it is not in the lane's skip list.

   This holds whatever `next`'s order says, and even when the fix's touches overlap a task in
   Doing (`Get-ReadyCiFixTaskId`). The lane holding that task rebases over the fix when it
   integrates, as it does over any other push. `task-board.ps1` is a guard file, so the board's
   order is left alone and the factory claims around it.
2. **Never shut out by a requeue.** The shift's requeued list keeps every lane of the shift off
   a requeued task. It no longer applies to a CI fix (`Get-ClaimSkip`). Only the lane that ran
   the fix skips it for the rest of the shift. `LaneRules.md` tells a lane's run never to send a
   CI fix back to wait for a task in Doing that holds its files. The run changes the files
   anyway.
3. **A lane of its own.** Once a minute, the coordinator checks origin's Backlog for a CI fix
   that has waited 2 minutes with no free lane (`Get-CiFixLaneDecision`). When it finds one, it
   adds a lane, up to 16. The new lane's first claim takes the fix. This is one lane above the
   pace and the machine cap, and the next lane step retires it once it is idle. Each CI fix gets
   at most one such lane every 10 minutes.
4. **Integrated ahead.** A lane integrating a CI fix waits for the integrate lock with
   `-Priority`: it keeps a marker beside the lock, refreshed every 3 seconds. While another
   lane's marker is under a minute old, other waiters do not try the lock. The fix therefore
   goes next once the holder lets go. A dead lane's marker goes stale within the minute.

## Consequences

- Most of each measured spell was time the factory controlled. With these rules, it is the
  claim (about a minute when a lane is free, at most 2 to 3 minutes otherwise), the fix's run,
  and one integration.
- Outside the factory's control: a CI run takes 11 to 39 minutes, including runner queue time,
  after the fix lands (24, 15 and 39 minutes in these spells). A failure that shows up only
  after the first fix (BL-4308 behind BL-4299) costs a second run. A spell therefore still
  passes 30 minutes when the fix's run plus one CI run does. Shortening the CI run is a
  `ci.yml` change, which belongs to the audit branch.
- A CI fix that overlaps a task in Doing can give that task's lane a rebase conflict. The lane
  resolves it with the lock released (BL-2967), as it would any other conflict.
- Adding a lane can briefly run one build more than the machine probe's cap.

## Amendment (BL-4412, AF-0502, 2026-10-31)

Decided by Claude under Stewart's delegation. Rule 3 cannot serve a CI fix filed after the
shift's `-Hours` deadline: lanes claim nothing once their time is up, so no lane is added then.
The next shift has to take it. The poll during the shift-end merge's CI wait (BL-3142) was
meant to start that shift, but it asked `Get-IdlePollDecision`, which says `stop` past the
deadline - and that poll always runs past it, because the lanes finish their last task after
the deadline. Shift 20261030-060635 (deadline 14:07) filed BL-4369 at 14:24 and BL-4370 at
14:27 while its last lanes finished. Its lanes ended at 14:44. Its merge then waited for CI
until 15:21, and the next shift started at 15:22.

Now a ready CI fix (`Get-ReadyCiFixTaskId`) is not held by the deadline: the merge-wait poll
starts the next shift for it beside the merge, at the first poll after the lanes end (every
`-IdlePollMinutes`). The trace line is "BL-#### is a ready CI fix, which the -Hours deadline
HH:mm does not hold (ADR-1158) during the merge's CI wait; next shift started (...)". Other
ready work still waits for the hand-over after the merge. A CI fix filed while the last lanes
run past the deadline still waits for them to end.
