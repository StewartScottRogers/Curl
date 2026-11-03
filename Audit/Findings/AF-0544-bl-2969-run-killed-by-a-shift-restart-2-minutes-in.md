---
id: AF-0544
title: BL-2969 run killed by a shift restart 2 minutes in; the new shift gave it to the wrong lane and it stayed orphaned in Doing
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-11-03_0025.md: the reproduction no longer reproduces.
key: process:logs:BL-2969:unfinished-run
reproduction: none
task: BL-4650
tasks: BL-4650
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed: 2026-11-03
closed-how: reliable-reaudit
closed-by: 2026-11-03_0025.md
---
# AF-0544 - BL-2969 run killed by a shift restart 2 minutes in; the new shift gave it to the wrong lane and it stayed orphaned in Doing

## Summary

Low finding from the process auditor at `logs/BL-2969-20261102-083146-L2.jsonl`: BL-2969 run killed by a shift restart 2 minutes in; the new shift gave it to the wrong lane and it stayed orphaned in Doing.

## Evidence

Location: `logs/BL-2969-20261102-083146-L2.jsonl`

unfinishedRuns lists BL-2969-20261102-083146-L2.jsonl: 56002 bytes, no result line, empty .err.txt. DarkFactory-20261102-083146-L2.log:137-140 has claim 09:49:11, build ok 09:50:17 and test 8582/8582 at 09:51:09. The coordinator then logged '09:51:12 shift end (restarted: replaced by a new shift)' (DarkFactory-20261102-083146.log), which killed the run. The new coordinator logged '09:51:19 BL-2969 adopt held by no lane; lane 1's worktree has uncommitted work, so that lane resumes it' and '09:51:47 lane 1 adopts BL-2969'. But the run had been in lane 2, and lane 1's uncommitted work was BL-4507's (L1 had claimed BL-4507 at 09:35:09). Lane 1 then logged '09:51:50 sync stash uncommitted work kept: git stash list' and claimed BL-4507 at 09:52:11, redoing it in 4.3 min after the coordinator had requeued it as 'no work for it anywhere'. BL-2969 is never claimed again in the window and is still in Tasks/Doing at the audited commit. The run used only Bash and Skill tools with no file edits, so no work was lost. The cost is a task stuck in Doing for about 8 hours and one task redone.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261102-172448\logs -CiRunsJson Z:\repos\Curl.audit\20261102-172448\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261102-172448\logs\DarkFactory-*.log -Pattern 'BL-2969|restarted|lane 1 adopts'
```

- Expected: unfinishedRuns empty; an adopted task is resumed by the lane whose worktree holds its work
- Actual: unfinishedRuns contains BL-2969-20261102-083146-L2.jsonl; lane 1 'adopts' BL-2969 at 09:51:47, then stashes and claims BL-4507; BL-2969 never resumes

## Re-audits

- 2026-11-03 | 2026-11-03_0025.md | reproduces: no | This window holds one shift (174304) that finished 79 tasks, with no 'restarted' shift end. It adopted 11 stopped lanes 'work in place', each resuming its own task. unfinishedRuns is empty. BL-2969 was claimed by L8 at 18:13:58 and DONE at 20:05:05. The old orphan's after-effect still cost this window two refused shift starts (17:25:01 and 17:40:37 Arizona, about 18 minutes with alarms), but no run was killed or orphaned again.

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_0025.md: the reproduction no longer reproduces.
