---
id: BL-4715
title: Fix AF-0567: CI red 54.97 minutes from run 41549222390 (6dfcd0aa): a stale known-failure line for Flow_FromTheBroker_IsAnsweredWithFlowOk, filed 32 minutes late because shift starts were refused over orphaned BL-2969
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-11-03
completed:
---
# BL-4715 — Fix AF-0567: CI red 54.97 minutes from run 41549222390 (6dfcd0aa): a stale known-failure line for Flow_FromTheBroker_IsAnsweredWithFlowOk, filed 32 minutes late because shift starts were refused over orphaned BL-2969

## Goal

The cause of the factory behaviour the audit office reported as AF-0567 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0567 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0567-ci-red-54-97-minutes-from-run-41549222390-6dfcd0aa.md`.

Location: `logs/DarkFactory-20261102-174304.log:31`

Location: `logs/DarkFactory-20261102-174304.log:31`

Spell 2026-11-03T00:22:12Z to 01:17:10Z, 54.97 minutes, from run 41549222390 on 6dfcd0aa (logs/ci-runs.json:797). Shift starts at 17:25:01 and 17:40:37 Arizona were refused: 'BL-2969 is in Doing and held by no lane' (DarkFactory-20261102-172455.log:1, DarkFactory-20261102-174031.log:1). CI watch first ran at 17:54:19 and filed BL-4584, 32 minutes after the run failed. L2 claimed it at 17:57:20 and finished in 2.5 minutes with nothing to change: 'its stale known-failure line was removed in 844d53972'. That commit (BL-4563, 00:56:31Z) was the real fix; it was integrated by L2 at 17:57:01 when it pushed the stopped lane's BL-4563 work. Runs 41556437882, 41556616280 and 41556972129 after it were cancelled; 41557110247 went green at 01:17:10Z. Cause: a lane integrated a fix that made an Extended conformance case pass without dropping its KnownFailures line.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261103-002553\logs -CiRunsJson Z:\repos\Curl.audit\20261103-002553\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 41549222390; Select-String -Path Z:\repos\Curl.audit\20261103-002553\logs\DarkFactory-20261102-1*.log -Pattern 'BL-4584|41549222390|refuse'
```

- Expected: No CI red spell over 30 minutes.
- Actual: Spell 2026-11-03T00:22:12Z to 01:17:10Z, 54.97 minutes, runId 41549222390; refused shift starts at 17:25:01 and 17:40:37; BL-4584 filed 17:54:19.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
