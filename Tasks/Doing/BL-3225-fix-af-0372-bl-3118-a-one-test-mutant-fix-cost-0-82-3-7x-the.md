---
id: BL-3225
title: Fix AF-0372: BL-3118, a one-test mutant fix, cost $0.82 (3.7x the median) on Opus with a refused Bash command and a by-hand mutate and revert
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed:
---
# BL-3225 — Fix AF-0372: BL-3118, a one-test mutant fix, cost $0.82 (3.7x the median) on Opus with a refused Bash command and a by-hand mutate and revert

## Goal

What drove the past run's cost that the audit office reported as AF-0372 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0372 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0372-bl-3118-a-one-test-mutant-fix-cost-0-82-3-7x-the-m.md`.

Location: `logs/BL-3118-20261022-143901-L1.jsonl`

Location: `logs/BL-3118-20261022-143901-L1.jsonl`

BL-3118 costUsd=0.8229, claims=1, against a bar of 0.662. Result: 18 turns, 6.0 min, cache read 721,945 and cache creation 74,007 tokens. Its change was one test (TryCheckUrlCredentials_PlainUrlCredentialsOnFtp_Passes). Lane trace: 14:48:53 'test FAIL Permission to use Bash with command head; git che...', then 14:49:03 edit TransferCredentialLookup.cs, 14:50:26 test 11/12 pass, 14:50:29 edit TransferCredentialLookup.cs (mutant by hand, then reverted). Routed to Opus because the title 'names security'. BL-3127, a fix of the same kind, ran on Sonnet ('routine feature: test-strength audit fix').

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261022-190247\logs\DarkFactory-20261022-143901-L1.log -Pattern 'BL-3118 (test|edit|end)'
```

- Expected: costUsd at most 3 times the median (0.662).
- Actual: BL-3118 costUsd=0.8229. The trace shows 'test FAIL Permission to use Bash', 2 edits of TransferCredentialLookup.cs, and 'end 18 turns, 6 min [model opus: feature pipeline, names security]'.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
