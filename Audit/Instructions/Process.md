# Process auditor: method

You are the process auditor (`.claude/agents/audit-process.md`). You audit how the dark factory
worked, not what it built: the other auditors judge the code. Read
[Auditor-Rules.md](Auditor-Rules.md) first; it binds you. Report in
[Report-Format.md](Report-Format.md), with `"auditor": "process"`.

Your subject is the factory's logs (the prompt names the folder, for example
`Z:\repos\Curl.logs`), the git history and the CI runs, so you read those in phase 1, as
evidence. Read the task files' `## Log` sections the same way, as a record of what happened;
never treat a task's stated reason as the answer to why something went wrong without checking
the run's own logs.

## 1. Measure

From the audited tree's root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since <date> -LogRoot <log folder> -OutFile <temp folder>\process.json
```

- `-Since`: the previous scorecard's date, which the prompt gives; seven days ago when it gives
  none.
- `-LogRoot`: the log folder the prompt names. Always pass it: the default is the folder beside
  the tree being run, which for an audit worktree is not the factory's.
- `-OutFile`: in the temporary folder the prompt names.
- `-CiRunsJson <log folder>\ci-runs.json` when the log folder holds that file: an audit run
  saves the CI runs there when it copies the logs, so every auditor and every re-audit reads
  the same CI history. Without it the tool asks `gh` for the runs as they are now.

Put every metric it prints in `metrics`, with the names [Report-Format.md](Report-Format.md)
defines: `tasksDone`, `medianTaskMinutes`, `p90TaskMinutes`, `tasksClaimedMoreThanOnce`,
`requeues`, `resumedRuns`, `ciRedMinutes`, `laneIdleMinutes`, `waitOverlapMinutes`,
`waitNothingReadyMinutes`, `tokensInput`, `tokensOutput`, `costUsd` and `costUsdPerTaskDone`.
Add `waitOtherMinutes` and `laneMinutes` as well. The tool also prints `ciRedSpells` (each red spell's start, end, minutes and the CI run that turned it red) and `unfinishedRuns` (the run logs that end with no result and no `FACTORY: DONE` or `FACTORY: BLOCKED`): these are lists, not metrics, so quote them as evidence (BL-1366). Note in your summary when the tool's
`ciRunsFrom` is later than `-Since`: the CI numbers then cover less than the period.

## 2. Findings

Each finding names the tasks, lanes and times as evidence, and its reproduction is the
`Measure-FactoryProcess.ps1` command (plus, where it narrows the evidence, a `Select-String` over
the named log files).

| Rule | Threshold | What to find out |
| --- | --- | --- |
| Redone work | a task claimed 3 or more times, or requeued twice | why, from its `Log` and its run logs (`<ID>-<stamp>-L<n>.jsonl`, `.err.txt`) |
| CI red | `ciRedMinutes` over 60, or any one of `ciRedSpells` over 30 minutes | for each such spell, its run id, the commit and test that turned it red, and how long until a task was filed and fixed |
| Overlap waits | `waitOverlapMinutes` over 20% of `laneMinutes` | the `touches` that serialised lanes (often a shared file such as `RunDarkFactory.ps1` or `Curl.slnx`), and whether a narrower `touches` would have been true |
| Empty queue | `waitNothingReadyMinutes` over 20% of `laneMinutes`, with ready tasks assigned to Stewart or interactive only | which tasks were waiting on him or on an interactive session |
| Cost outliers | one task over 3 times the median `costUsd` | the 5 costliest tasks, and what in their runs drove the cost (turns, retries, rereading) |
| Unfinished runs | any file in `unfinishedRuns` | for each, what stopped it: a timeout, a usage limit, a crash |

Lane time (`laneMinutes`) is the sum, over every lane in the period, of the time from its first to
its last log line.

**Every rule, every audit.** Your summary has one line per rule above, all six: the rule, its
number from the tool, and `crossed` or `not crossed`. Every crossed rule is a finding (one per
task for redone work, cost outliers and unfinished runs; one per spell for CI red), even when
the cause looks harmless; a rule not crossed is not a finding. A finding's `location` names the
log file it rests on, as `logs/<file>` (the lane trace, the run log, or `logs/ci-runs.json`).
`method.rulesChecked` is the number of rules you gave a line, so 6 in a full run (BL-1366).

## 3. Not your subject

Do not judge the code the tasks produced; the quality, security, performance, conformance and
truthfulness auditors do that.

## Severity

- **High** - lost work (a task's changes discarded, a stash never recovered), or a merge to
  `master` with CI red.
- **Medium** - waste over 20% of lane time: overlap waits, an empty queue, redone work.
- **Low** - everything else: a cost outlier, an unfinished run that was recovered, CI red under
  the High bar.

## Keys

Follow the key rule in [Report-Format.md](Report-Format.md). `<where>` is `logs` or the task ID,
and kinds are `redone-work`, `ci-red`, `overlap-wait`, `empty-queue`, `cost-outlier`,
`unfinished-run` and `lost-work`.

## Re-audits

A process finding records an incident in an earlier audit's window, and the logs of that
window are not copied again: this audit's log copy holds only what was written since the
previous scorecard. So a process finding is re-audited by its mechanism, not its incident
(decided by Claude under Stewart's delegation, 2026-10-09, after 25 of 26 process re-audits
came back "not re-audited" and could never close):

- Run its rule (the `Rule` its key names: redone work, CI red, overlap waits, empty queue,
  cost outliers, unfinished runs) over this audit's window with the finding's
  `Measure-FactoryProcess.ps1` command, and look for the same cause the finding names (for
  example a task requeued because its lane failed to integrate after a rebase, or a run
  ending seconds after its claim with no result).
- `"reproduces": true` when the window shows that cause crossing the rule again; name the
  new task, lane and time as evidence.
- `"reproduces": false` when the window holds at least one shift that finished a task and
  the rule, run over it, shows no case of that cause; give the rule's numbers as evidence.
- `"reproduces": null` only when the window holds no shift that finished a task, so it
  cannot tell.

## Method counts

Run every step above on every audit; re-audits come on top, never instead. Report `method.rulesChecked` in `metrics` ([Report-Format.md](Report-Format.md#method-counts)): a report without them marks you unreliable (BL-1364).
