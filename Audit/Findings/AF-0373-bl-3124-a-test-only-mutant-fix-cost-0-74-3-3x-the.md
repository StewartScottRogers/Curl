---
id: AF-0373
title: BL-3124, a test-only mutant fix, cost $0.74 (3.3x the median) on Opus with a refused Bash command and a by-hand mutate and revert
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-3124:cost-outlier
reproduction: none
task: BL-3226
tasks: BL-3226
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0373 - BL-3124, a test-only mutant fix, cost $0.74 (3.3x the median) on Opus with a refused Bash command and a by-hand mutate and revert

## Summary

Low finding from the process auditor at `logs/BL-3124-20261022-143901-L6.jsonl`: BL-3124, a test-only mutant fix, cost $0.74 (3.3x the median) on Opus with a refused Bash command and a by-hand mutate and revert.

## Evidence

Location: `logs/BL-3124-20261022-143901-L6.jsonl`

BL-3124 costUsd=0.7386, claims=1, against a bar of 0.662. Result: 25 turns, 5.4 min, cache read 942,912 tokens, 16 Bash calls. Lane trace: 15:07:57 'test FAIL Permission to use Bash with command head -5; git ...', then 15:08:05 and 15:08:25 edits of OpenSslCipherSuites.cs (mutant in by hand, 71/73 pass, reverted). It also made 3 separate edits of the task file (15:10:38 to 15:10:42). Routed to Opus because the title 'names crypto', though the fix was test rows only.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261022-190247\logs\DarkFactory-20261022-143901-L6.log -Pattern 'BL-3124 (test|edit|end)'
```

- Expected: costUsd at most 3 times the median (0.662).
- Actual: BL-3124 costUsd=0.7386. The trace shows 'test FAIL Permission to use Bash', 2 edits of OpenSslCipherSuites.cs, and 'end 25 turns, 5.4 min [model opus: feature pipeline, names crypto]'.

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
