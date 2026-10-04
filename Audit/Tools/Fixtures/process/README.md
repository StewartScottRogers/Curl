# Process fixture

A small synthetic dark factory log set with known answers, for
`Measure-FactoryProcess.ps1 -SelfTest` (BL-1008). Nothing here is real.

- `lane-output/DarkFactory-20260101-100000-L1.log` and `-L2.log`: two lanes. BL-001 is claimed and
  done in 30 minutes; BL-002 is claimed, requeued, left waiting 20 minutes on an overlap,
  claimed again and done in 20 minutes; BL-003 is done in 45 minutes, then lane 2 waits 10
  minutes with nothing ready; BL-004 is claimed and never finishes; BL-005 is claimed at
  23:50 and done at 00:20 the next day (30 minutes).
- `lane-output/*.jsonl`: one `result` event per run; BL-002 has a resumed run; BL-004's run\n  (`BL-004-20260101-100000-L2.jsonl`) has no `result` event and no `FACTORY:` line.
- `ci-runs.json`: a canned `gh run list`: red from 10:10 to 11:10 (a cancelled and an
  unfinished run ignored), and from 12:05 to 13:05.

Known answers: tasksDone 4; medianTaskMinutes 30; p90TaskMinutes 45;
tasksClaimedMoreThanOnce 1; requeues 1; resumedRuns 1; ciRedMinutes 120; laneIdleMinutes
30 (waitOverlapMinutes 20, waitNothingReadyMinutes 10, waitOtherMinutes 0); tokensInput
5295; tokensOutput 240; costUsd 4.5; costUsdPerTaskDone 1.125; laneMinutes 941.42;\nciRedSpells run 2 (10:10 to 11:10, 60 minutes) and run 6 (12:05 to 13:05, 60 minutes);\nunfinishedRuns BL-004-20260101-100000-L2.jsonl (BL-1366).

The folder is `lane-output`, not `logs`: the repository ignores every `logs/` folder.
The two `.log` traces are added with `git add -f`: they are synthetic fixtures, not logs, but the
`*.log` ignore rule would otherwise leave them out.
