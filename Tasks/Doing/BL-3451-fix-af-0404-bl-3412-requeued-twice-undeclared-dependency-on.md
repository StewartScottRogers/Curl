---
id: BL-3451
title: Fix AF-0404: BL-3412 requeued twice (undeclared dependency on BL-3410, then cost cap with work stashed) and then split
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-24
completed:
---
# BL-3451 — Fix AF-0404: BL-3412 requeued twice (undeclared dependency on BL-3410, then cost cap with work stashed) and then split

## Goal

The cause of the factory behaviour the audit office reported as AF-0404 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0404 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0404-bl-3412-requeued-twice-undeclared-dependency-on-bl.md`.

Location: `logs/DarkFactory-20261023-171352-L3.log:25`

Location: `logs/DarkFactory-20261023-171352-L3.log:25`

Claim 1, L4 14:44:45 (opus, 25 turns, $0.73): REQUEUE 14:55:22 'Depends on BL-3410: both cases run runtests.pl ... criteria were template placeholders' (DarkFactory-20261023-110315-L4.log:237). Claim 2, L3 17:16:23 (opus retry, 47 turns, $2.19): 'stash uncommitted work kept' at 17:26:06, then REQUEUE 'Code done and Conformance tests green (stash); cost cap reached before the quality measurement and solution fast tests'. At 19:13 lanes L1, L2 and L3 all logged it 'unclaimable cost cap: 2.92 of its 3.00'. L1 split it into BL-3438 (land the stashed work, $1.25) and BL-3439. Cause: the task was ready with placeholder criteria and no declared dependency on BL-3410, so its first Opus run was wasted. The second run ran out of cap before the gates. Measured cost $2.92 (6.7x the median).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-*.log -Pattern 'BL-3412 (claim|REQUEUE|stash|split|unclaimable)'
```

- Expected: BL-3412 claimed once, after BL-3410 was done, and finished within its cap
- Actual: two claims, two REQUEUEs (dependency, then cost cap with a stash), three unclaimable lines and a split into BL-3438 and BL-3439

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Interactive session: fixing the prerequisite-requeue cost-cap mechanism (ADR-0811) Criteria at claim: 87cd20bd 9742157d.
