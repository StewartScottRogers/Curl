---
id: AF-0206
title: BL-2134 cost $2.34, 3.2 times the median, including a repair run after its own run left test1462 red
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-14_0453.md: the reproduction no longer reproduces.
key: process:logs:BL-2134:cost-outlier
reproduction: none
task: BL-2199
tasks: BL-2199
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed: 2026-10-14
closed-how: reliable-reaudit
closed-by: 2026-10-14_0453.md
---
# AF-0206 - BL-2134 cost $2.34, 3.2 times the median, including a repair run after its own run left test1462 red

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261013-095305-L1.log:22`: BL-2134 cost $2.34, 3.2 times the median, including a repair run after its own run left test1462 red.

## Evidence

Location: `logs/DarkFactory-20261013-095305-L1.log:22`

Measure-FactoryProcess.ps1: BL-2134 costUsd 2.3368, 10.93 minutes, against a $0.73 median. The cost is two runs. The main run (BL-2134-20261013-095305-L1.jsonl) took 38 turns and $1.55, with 25 Bash calls, 19 of them slice reads. The repair run (BL-2134-20261013-095305-L1-repair.jsonl) took 22 turns and $0.78. The lane log shows 12:14:37 'flaky? fast tests red (Curl.Conformance.UnitTests: test1462)' and 12:15:00 'repair fast tests failed twice', then green at 12:19:20 after the repair took test1462 off the ratchet (5dbe71d11). test1462 already belonged to BL-2136, filed for the same CI run, so the repair work overlapped another task's scope.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-13 -LogRoot Z:\repos\Curl.audit\20261013-182203\logs -CiRunsJson Z:\repos\Curl.audit\20261013-182203\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2134'; Select-String -Path Z:\repos\Curl.audit\20261013-182203\logs\DarkFactory-20261013-095305-L1.log -Pattern 'BL-2134 (flaky|repair|end)'
```

- Expected: No task over 3 times the median costUsd (about $2.19).
- Actual: BL-2134 costUsd 2.3368. The log shows two 'end' lines (38 and 22 turns) around 'flaky?' and two 'repair' lines.

## Re-audits

- 2026-10-14 | 2026-10-14_0453.md | reproduces: no | Original reproduction still prints BL-2134 $2.3368 with its 'flaky?' and 'repair' lines from 2026-10-13 12:14-12:19. Over this window (53 tasks done) the only cost outlier is BL-2182, a single run with no repair. grep for 'repair' and 'flaky?' across every DarkFactory-*.log in the window's log folder finds nothing.

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
- 2026-10-14: accepted -> closed. Re-audit 2026-10-14_0453.md: the reproduction no longer reproduces.
