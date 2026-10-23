---
id: AF-0372
title: BL-3118, a one-test mutant fix, cost $0.82 (3.7x the median) on Opus with a refused Bash command and a by-hand mutate and revert
auditor: process
severity: Low
status: closed
reason: Audit 2026-10-23_0618.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3225) are Done at 32b1dda228a5, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
key: process:logs:BL-3118:cost-outlier
reproduction: none
task: BL-3225
tasks: BL-3225
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed: 2026-10-23
closed-how: task-done-historical
closed-by: 2026-10-23_0618.md
---
# AF-0372 - BL-3118, a one-test mutant fix, cost $0.82 (3.7x the median) on Opus with a refused Bash command and a by-hand mutate and revert

## Summary

Low finding from the process auditor at `logs/BL-3118-20261022-143901-L1.jsonl`: BL-3118, a one-test mutant fix, cost $0.82 (3.7x the median) on Opus with a refused Bash command and a by-hand mutate and revert.

## Evidence

Location: `logs/BL-3118-20261022-143901-L1.jsonl`

BL-3118 costUsd=0.8229, claims=1, against a bar of 0.662. Result: 18 turns, 6.0 min, cache read 721,945 and cache creation 74,007 tokens. Its change was one test (TryCheckUrlCredentials_PlainUrlCredentialsOnFtp_Passes). Lane trace: 14:48:53 'test FAIL Permission to use Bash with command head; git che...', then 14:49:03 edit TransferCredentialLookup.cs, 14:50:26 test 11/12 pass, 14:50:29 edit TransferCredentialLookup.cs (mutant by hand, then reverted). Routed to Opus because the title 'names security'. BL-3127, a fix of the same kind, ran on Sonnet ('routine feature: test-strength audit fix').

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261022-190247\logs\DarkFactory-20261022-143901-L1.log -Pattern 'BL-3118 (test|edit|end)'
```

- Expected: costUsd at most 3 times the median (0.662).
- Actual: BL-3118 costUsd=0.8229. The trace shows 'test FAIL Permission to use Bash', 2 edits of TransferCredentialLookup.cs, and 'end 18 turns, 6 min [model opus: feature pipeline, names security]'.

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-23: accepted -> closed. Audit 2026-10-23_0618.md: a cost-outlier finding records a past run's cost, which no fix can change; its tasks (BL-3225) are Done at 32b1dda228a5, so it closes without a re-audit (ADR-0422, amended 2026-10-14).
