# Process fixture

A small synthetic dark factory log set with known answers, for
`Measure-FactoryProcess.ps1 -SelfTest` (BL-1008). Nothing here is real.

- `lane-output/DarkFactory-20260101-100000-L1.log` and `-L2.log`: two lanes. BL-001 is claimed and
  done in 30 minutes; BL-002 is claimed, requeued, left waiting 20 minutes on an overlap,
  claimed again and done in 20 minutes; BL-003 is done in 45 minutes, then lane 2 waits 10
  minutes with nothing ready; BL-004 is claimed and never finishes; BL-005 is claimed at
  23:50 and done at 00:20 the next day (30 minutes).
- `lane-output/*.jsonl`: one `result` event per run; BL-002 has a resumed run; BL-004's run
  (`BL-004-20260101-100000-L2.jsonl`) has no `result` event and no `FACTORY:` line.
- `lane-output/DarkFactory-20260102-100000.log` and `BL-006-20260102-100000-L1.jsonl`: a shift log
  with no `shift end` line and a run of it with no `result` event, so the run was still running
  when the logs were read (AF-0269).
- `ci-runs.json`: a canned `gh run list`: red from 10:10 to 11:10 (a cancelled and an
  unfinished run ignored), from 12:05 to 13:05, and from 15:00 to 15:30. Runs 9, 10 and 11 failed
  between 13:05 and 15:30, but only run 11 counts as red (ADR-1235, BL-4825).
- `ci-jobs.json`: the failed runs' jobs and annotations. Run 9's only failure is a macOS job no
  runner acquired; run 10's is the audit guard's checkout running past its 5-minute timeout; run
  11 has a failing `Fast tests` step (and a macOS job no runner acquired). Runs 2, 3 and 6 have no
  entry, so they count as red as before.

Known answers: tasksDone 4; medianTaskMinutes 30; p90TaskMinutes 45;
tasksClaimedMoreThanOnce 1; requeues 1; resumedRuns 1; ciRedMinutes 150; laneIdleMinutes
30 (waitOverlapMinutes 20, waitNothingReadyMinutes 10, waitOtherMinutes 0); tokensInput
5295; tokensOutput 240; costUsd 4.5; costUsdPerTaskDone 1.125; laneMinutes 941.42;
ciRedSpells run 2 (10:10 to 11:10, 60 minutes), run 6 (12:05 to 13:05, 60 minutes) and run 11
(15:00 to 15:30, 30 minutes); ciInfrastructureRuns runs 9 and 10 (BL-4825);
unfinishedRuns BL-004-20260101-100000-L2.jsonl (BL-1366); runningRuns
BL-006-20260102-100000-L1.jsonl (AF-0269).

The folder is `lane-output`, not `logs`: the repository ignores every `logs/` folder.
The `.log` traces are added with `git add -f`: they are synthetic fixtures, not logs, but the
`*.log` ignore rule would otherwise leave them out.
