---
id: BL-3453
title: Fix AF-0406: BL-3234 requeued twice waiting on unfinished prerequisite tasks, then split over its cost cap
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
# BL-3453 — Fix AF-0406: BL-3234 requeued twice waiting on unfinished prerequisite tasks, then split over its cost cap

## Goal

The cause of the factory behaviour the audit office reported as AF-0406 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0406 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0406-bl-3234-requeued-twice-waiting-on-unfinished-prere.md`.

Location: `logs/DarkFactory-20261023-110315-L1.log:11`

Location: `logs/DarkFactory-20261023-110315-L1.log:11`

Claim 1, L3 04:41:42 (opus feature, 28 turns): REQUEUE 04:46:47 'Waiting on BL-3284 to BL-3288 (the protocol and proxy server ports, split out for size) and BL-3235'. Claim 2, L1 11:04:17 (opus retry, 24 turns): REQUEUE 11:08:44 'Waiting on BL-3334 (the SOCKS4/SOCKS5 half of proxy_server.py)'. At 17:14:44 L3 logged it 'unclaimable cost cap 1.97 of 3.07' and split it into BL-3424..BL-3427 at 17:16:23. Both Opus runs did nothing but find that a prerequisite task was not done. The board offered it as ready without the dependency declared.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-*.log -Pattern 'BL-3234 (claim|REQUEUE|split|unclaimable)'
```

- Expected: BL-3234 not claimable until BL-3284..3288, BL-3235 and BL-3334 were done
- Actual: two claims each ending in a REQUEUE naming an unfinished prerequisite, then unclaimable and split

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Interactive session: fixing the prerequisite-requeue cost-cap mechanism (ADR-0811) Criteria at claim: 87cd20bd 9742157d.
