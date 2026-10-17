---
id: AF-0227
title: Quality gates job red for 26.83 minutes from run 38700283564 on 639a0aec, BL-2268's finish commit
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:CI-run-38700283564:ci-red
reproduction: none
task: BL-2280
tasks: BL-2280
found: 2026-10-15
found-at: 3e1062e68644127a82ba2f5ef33ceb1044cc6f67
scorecard: 2026-10-15_0156.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0227 - Quality gates job red for 26.83 minutes from run 38700283564 on 639a0aec, BL-2268's finish commit

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: Quality gates job red for 26.83 minutes from run 38700283564 on 639a0aec, BL-2268's finish commit.

## Evidence

Location: `logs/ci-runs.json`

The CI red rule is crossed (ciRedMinutes 170.52 over 60). Spell 2026-10-15T07:50:16Z to 08:17:06Z, 26.83 min, runId 38700283564: a failure on 639a0aec (chore(tasks): finish BL-2268) in job Quality gates (windows-latest), step Measure coverage, complexity and CRAP. A sibling run on the same sha, 38700277194, passed. The shift-end merge was withheld (DarkFactory-20261014-215526.log 00:50:41 'not merged: CI failure on 639a0ae'). BL-2269 was filed at 00:51:09 and finished at 01:07:03 ('Quality job failure was a lost coverage re...', DarkFactory-20261015-005118-L1.log). Run 38703393249 on 08cb9f24 turned green, and 08cb9f2 was merged to master at 01:18:03 after green, so master got no red merge. This is the second Quality gates measure-step failure in the window (with run 38666857143).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-14 -LogRoot Z:\repos\Curl.audit\20261015-015611\logs -CiRunsJson Z:\repos\Curl.audit\20261015-015611\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table
```

- Expected: ciRedMinutes 60 or less.
- Actual: ciRedMinutes 170.52; spell 07:50:16Z to 08:17:06Z, 26.83 min, runId 38700283564.

## Re-audits

- 2026-10-16 | 2026-10-16_1623.md | reproduces: yes | Ran the reproduction: the earlier window still shows 07:50:16Z-08:17:06Z, 26.83 min, run 38700283564 on 639a0aec. By mechanism over this window (ciRedMinutes 181.44 > 60), the Quality gates (windows-latest) job went red again on a lane commit: run 38876049958 on 241c183e, 103.15 min (2026-10-16T09:02:13Z-10:45:22Z). The cause was a Libcurl CurlMulti.SetOption branch gap that a lane had landed; it was fixed by BL-2467 at 03:01:55 local (DarkFactory-20261015-200601-L1.log:437).

## Log

- 2026-10-15: filed proposed.
- 2026-10-15: proposed -> accepted.
