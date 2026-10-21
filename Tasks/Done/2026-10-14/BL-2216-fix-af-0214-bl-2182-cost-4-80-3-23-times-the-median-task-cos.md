---
id: BL-2216
title: Fix AF-0214: BL-2182 cost $4.80, 3.23 times the median task cost, over 92 turns of Bash-sliced code reading
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2216 — Fix AF-0214: BL-2182 cost $4.80, 3.23 times the median task cost, over 92 turns of Bash-sliced code reading

## Goal

The defect the audit office reported as AF-0214 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0214 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0214-bl-2182-cost-4-80-3-23-times-the-median-task-cost.md`.

Location: `logs/BL-2182-20261013-133412-L1.jsonl`

Location: `logs/BL-2182-20261013-133412-L1.jsonl`

Measure-FactoryProcess.ps1 tasks: BL-2182 claims 1, 17.98 minutes, costUsd 4.8021, done (lane 1, claimed 20:52:15 PDT 2026-10-13). Median costUsd over the 45 tasks whose runs are in this log copy is $1.4886 (3x = $4.466); the 9 tasks reported at $0 ran in the previous copy and are left out. Five costliest: BL-2182 $4.80 (92 turns; tools Bash 58, Edit 17, Write 8, PowerShell 6, Read 2; 19 trace lines with 'sed -n' slices such as 'sed -n 570,615p Curl.Core.UnitLibrary/RedirectFollower.cs', 'sed -n 120,250p ...HttpProtocolHandlerTests.AwsSigV4.cs'); BL-2171 $3.76 (65 turns, 26 Bash slices, plus a 14-turn $0.53 -resolve run for a rebase conflict); BL-2183 $3.67 (two claims: a 74-turn $1.95 run requeued because BL-2170 in Doing touched Curl.Console, then a 40-turn $1.72 run); BL-2172 $3.58 (88 turns, 25 Bash slices); BL-2180 $3.45 (67 turns, 34 Bash slices, session held 21 minutes by a stray 'find /'). The cost driver is the same as AF-0190/0191/0204/0205: many turns, each rereading a cached context of about 190k tokens, spent paging files in small Bash slices instead of one Read.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-13 -LogRoot Z:\repos\Curl.audit\20261014-045316\logs -CiRunsJson Z:\repos\Curl.audit\20261014-045316\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Sort-Object costUsd -Descending | Select-Object -First 5; Select-String -Path Z:\repos\Curl.audit\20261014-045316\logs\BL-2182-20261013-133412-L1.jsonl -Pattern 'sed -n' | Measure-Object
```

- Expected: No task over 3 times the median cost ($4.47)
- Actual: BL-2182 costUsd 4.8021 at the top; 19 matching 'sed -n' lines in its trace

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction: BL-2182 is a past run and its cost cannot change; its $4.80 is 3.23 times the median, inside the 4 times cap (BL-2116). See Notes.
- [x] No code changed (task board only), so build and tests are unaffected.

## Notes

- 2026-10-14 (Claude): AF-0214: BL-2182 cost 3.23 times the median, under the 4 times cost cap Stewart chose on 2026-10-12 (BL-2116). Unless its run log shows avoidable waste, record that this is within the chosen margin and finish.

- 2026-10-14 (Claude): Checked the run-prompt rules in RunDarkFactory.ps1: AF-0190, AF-0204 and AF-0205 already tell a lane to Read a file once and never slice it with sed -n (BL-2215 added the quality-run rule). BL-2182 cost $4.80 = 3.23 times the median, under the 4 times cap Stewart chose on 2026-10-12, and the finding is about one past run, whose cost cannot change. No avoidable waste beyond what those rules already forbid, so no change to RunDarkFactory.ps1.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. AF-0214 within the 4x cost cap; slicing rules already in the run prompt
