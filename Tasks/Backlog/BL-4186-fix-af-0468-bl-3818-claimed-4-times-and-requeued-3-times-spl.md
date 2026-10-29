---
id: BL-4186
title: Fix AF-0468: BL-3818 claimed 4 times and requeued 3 times, splitting off BL-4026, BL-4044 and BL-4056 one run at a time
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-29
completed:
---
# BL-4186 — Fix AF-0468: BL-3818 claimed 4 times and requeued 3 times, splitting off BL-4026, BL-4044 and BL-4056 one run at a time

## Goal

The cause of the factory behaviour the audit office reported as AF-0468 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0468 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0468-bl-3818-claimed-4-times-and-requeued-3-times-split.md`.

Location: `logs/DarkFactory-20261029-014637-L2.log:41`

Location: `logs/DarkFactory-20261029-014637-L2.log:41`

The task Log shows 4 claims (2026-10-28 to 10-29); the tool reports claims=3, outcome requeued, cost $0.89. Requeues: 19:52:56 'Needs BL-4026 (ConnectTarget SNI host ...)' (DarkFactory-20261028-172341-L8.log:67); 22:03:05 'Code for the ldaps second connection landed; real-curl measurement and NTLM flags left to BL-4044' (DarkFactory-20261028-201954-L4.log:41); 02:44:44 'the --cacert variant is BL-4056's work' (DarkFactory-20261029-014637-L2.log:41). Done on claim 4. Each run found the next missing piece, so scoping happened across claims instead of before the first.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; Select-String -Path Z:/repos/Curl.audit/20261029-060658/logs/DarkFactory-*-L*.log -Pattern 'BL-3818 (claim|REQUEUE|DONE)'
```

- Expected: Fewer than 3 claims and fewer than 2 requeues.
- Actual: BL-3818: 3 REQUEUE lines; 4 claims in its task Log.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
