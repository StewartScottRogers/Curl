---
id: AF-0289
title: BL-2783 cost $3.43, 3.3 times the median task cost of $1.04 (12 builds, 2 of them failed on tooling)
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-2783:cost-outlier
reproduction: none
task: BL-2816
tasks: BL-2816
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0289 - BL-2783 cost $3.43, 3.3 times the median task cost of $1.04 (12 builds, 2 of them failed on tooling)

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261018-103803-L1.log`: BL-2783 cost $3.43, 3.3 times the median task cost of $1.04 (12 builds, 2 of them failed on tooling).

## Evidence

Location: `logs/DarkFactory-20261018-103803-L1.log`

Measure-FactoryProcess.ps1 -Since 2026-10-18: BL-2783 claims=1 minutes=20.87 costUsd=3.4339 outcome=done; threshold 3.1176. BL-2783-20261018-103803-L1.jsonl result: num_turns=69, 7672177 cache-read tokens, 67 tool calls (46 Bash), model opus. The lane log shows 12 build lines between 16:30:42 and 16:47:27. Two of them failed on the environment, not the code: '16:32:28 L1 BL-2783 build FAIL Permission to use Bash with command head -30; git~' and '16:34:34 L1 BL-2783 build FAIL rm: cannot remove 'Curl.Conformance.UnitTests/bin~'. That is avoidable waste: a denied command and a locked bin folder each cost a rebuild turn, and 10 builds ran for one change.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261018-190531\logs -CiRunsJson Z:\repos\Curl.audit\20261018-190531\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2783'; Select-String -Path Z:\repos\Curl.audit\20261018-190531\logs\DarkFactory-20261018-103803-L1.log -Pattern 'BL-2783 build'
```

- Expected: costUsd at most 3 times the median (3.1176).
- Actual: BL-2783 costUsd 3.4339; 12 build lines, 2 FAIL on permission denial and a locked bin folder.

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
