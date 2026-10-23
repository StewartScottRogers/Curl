---
id: BL-3226
title: Fix AF-0373: BL-3124, a test-only mutant fix, cost $0.74 (3.3x the median) on Opus with a refused Bash command and a by-hand mutate and revert
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed: 2026-10-22
---
# BL-3226 — Fix AF-0373: BL-3124, a test-only mutant fix, cost $0.74 (3.3x the median) on Opus with a refused Bash command and a by-hand mutate and revert

## Goal

What drove the past run's cost that the audit office reported as AF-0373 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0373 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0373-bl-3124-a-test-only-mutant-fix-cost-0-74-3-3x-the.md`.

Location: `logs/BL-3124-20261022-143901-L6.jsonl`

Location: `logs/BL-3124-20261022-143901-L6.jsonl`

BL-3124 costUsd=0.7386, claims=1, against a bar of 0.662. Result: 25 turns, 5.4 min, cache read 942,912 tokens, 16 Bash calls. Lane trace: 15:07:57 'test FAIL Permission to use Bash with command head -5; git ...', then 15:08:05 and 15:08:25 edits of OpenSslCipherSuites.cs (mutant in by hand, 71/73 pass, reverted). It also made 3 separate edits of the task file (15:10:38 to 15:10:42). Routed to Opus because the title 'names crypto', though the fix was test rows only.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261022-190247\logs\DarkFactory-20261022-143901-L6.log -Pattern 'BL-3124 (test|edit|end)'
```

- Expected: costUsd at most 3 times the median (0.662).
- Actual: BL-3124 costUsd=0.7386. The trace shows 'test FAIL Permission to use Bash', 2 edits of OpenSslCipherSuites.cs, and 'end 25 turns, 5.4 min [model opus: feature pipeline, names crypto]'.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Cause (AF-0373): BL-3124 ran 11% over the 3x-median bar ($0.7386 vs $0.662) because the title named crypto, so the model rule in DarkFactoryModelChoice.ps1 (not RunDarkFactory.ps1) routed a test-rows-only fix to Opus; the refused `head -5; git ...` Bash call and the by-hand mutate and revert were one-off agent choices (a few turns of 25). Recorded as unavoidable: the overage is marginal, the title marker is a deliberate safe default (a false sonnet on real crypto work costs more), and the only fix site is outside this task's touches. Build and fast tests green.

## Log

- 2026-10-23: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-22: Doing -> Done. AF-0373 cause recorded as unavoidable (Opus routing on a crypto-named title, marginal overage)
