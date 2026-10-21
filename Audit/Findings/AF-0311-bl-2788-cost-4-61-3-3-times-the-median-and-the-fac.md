---
id: AF-0311
title: BL-2788 cost $4.61, 3.3 times the median, and the factory never finished it
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-2788:cost-outlier
reproduction: none
task: BL-2949
tasks: BL-2949
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0311 - BL-2788 cost $4.61, 3.3 times the median, and the factory never finished it

## Summary

Low finding from the process auditor at `logs/BL-2788-20261018-192454-L3.jsonl`: BL-2788 cost $4.61, 3.3 times the median, and the factory never finished it.

## Evidence

Location: `logs/BL-2788-20261018-192454-L3.jsonl`

Run 1: $4.07, 79 turns, 19 min, 8.77M cache-read tokens, 45 Bash and 23 Edit calls. Its passing work was stashed when a dependency (BL-2789) turned up mid-run. Run 2 (BL-2788-20261019-061607-L1.jsonl): $0.53, 8 turns. The stash did not apply cleanly and the task was split. The lanes spent $4.61 and never finished the task; an interactive session completed it outside the cost cap. What drove the cost: one long run whose result was not integrated, then a split.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2788'
```

- Expected: costUsd at most 4.2465 (3 times the median).
- Actual: BL-2788 claims 4, minutes 0.27, costUsd 4.6076, blocked

## Re-audits

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
