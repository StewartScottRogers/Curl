---
id: AF-0412
title: BL-3418 cost $2.91 (6.6x the median) in one 49-turn Opus feature run porting checksrc.pl lines 900-1268
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-3418:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0412 - BL-3418 cost $2.91 (6.6x the median) in one 49-turn Opus feature run porting checksrc.pl lines 900-1268

## Summary

Low finding from the process auditor at `logs/BL-3418-20261023-110315-L3.jsonl`: BL-3418 cost $2.91 (6.6x the median) in one 49-turn Opus feature run porting checksrc.pl lines 900-1268.

## Evidence

Location: `logs/BL-3418-20261023-110315-L3.jsonl`

One run: opus feature pipeline, 49 turns, 14.9 min, $2.90, 5.02M cache-read tokens, 40.9k output tokens. One failing test check along the way ('test 132/134 pass' at 15:54:40). Done at 16:03:52. Cost driver: Opus rereading a large, growing context for 49 turns on a port of upstream script lines, the kind of routine port CLAUDE.md says goes to sonnet.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-20261023-110315-L3.log -Pattern 'BL-3418 (claim|test|end|DONE)'
```

- Expected: no task over 3 times the median costUsd
- Actual: BL-3418 costUsd 2.905; claim line shows '[model opus: feature pipeline]', end '49 turns, 14.9 min'

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
