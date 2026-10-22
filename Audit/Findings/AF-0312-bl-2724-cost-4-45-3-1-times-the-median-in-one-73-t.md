---
id: AF-0312
title: BL-2724 cost $4.45, 3.1 times the median, in one 73-turn run
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-2724:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0312 - BL-2724 cost $4.45, 3.1 times the median, in one 73-turn run

## Summary

Low finding from the process auditor at `logs/BL-2724-20261018-192454-L1.jsonl`: BL-2724 cost $4.45, 3.1 times the median, in one 73-turn run.

## Evidence

Location: `logs/BL-2724-20261018-192454-L1.jsonl`

One run: claim at 04:45:35, end at 05:08:14 (22.6 min), DONE at 05:09:35. 73 turns, 9.26M cache-read tokens, 43K output. Tools: 35 Bash (7 grep, 3 sed, 3 cat, many of them over the upstream curl sources it unpacked to /tmp/up2724: ftp.c, pingpong.c, ftpserver.pl), 13 Edit, 6 Grep, 4 PowerShell, and the same two test files read twice. What drove the cost: researching upstream's FTP response-timeout behaviour inside the run, with the context re-read every turn. No retries.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2724'
```

- Expected: costUsd at most 4.2465 (3 times the median).
- Actual: BL-2724 claims 1, minutes 24, costUsd 4.4497, done

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: yes | The original reproduction still gives BL-2724 claims=1, costUsd=4.4497. The same cause, one long run, recurs in this window: BL-2924 had one 83-turn run (BL-2924-20261021-064209-L4.jsonl, $5.06, 10.9M cache-read tokens) plus a $0.51 resolve, $5.56 in total, 3.1 times the median.

## Log

- 2026-10-19: filed proposed.
