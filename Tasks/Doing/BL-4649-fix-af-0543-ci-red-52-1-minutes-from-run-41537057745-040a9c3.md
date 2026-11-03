---
id: BL-4649
title: Fix AF-0543: CI red 52.1 minutes from run 41537057745 (040a9c35): Windows quality gate failed on AMQP 1.0 container members a lane integrated over the gates
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
# BL-4649 — Fix AF-0543: CI red 52.1 minutes from run 41537057745 (040a9c35): Windows quality gate failed on AMQP 1.0 container members a lane integrated over the gates

## Goal

The cause of the factory behaviour the audit office reported as AF-0543 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0543 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0543-ci-red-52-1-minutes-from-run-41537057745-040a9c35.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-11-02T22:59:50Z (15:59 Arizona), end 23:51:56Z (16:51), 52.1 min, runId 41537057745 on 040a9c352. Failure: job 'Quality gates (windows-latest) step Measure coverage, complexity and CRAP' (DarkFactory-20261102-095114.log:151, 16:02:02). The members outside the gates were Amqp1ReferenceContainer.Accepts and AnswerAsync, from 9598845a7 (14:28, BL-4545). L7's quality checks on that task reported 'ok' (DarkFactory-20261102-095114-L7.log, 14:22:43 to 14:25:19), while the split of those very members was left to BL-4546, and that split's parent BL-4527 had been requeued at 12:52 with '9 Amqp1 members are still over the complexity or branch gate'. BL-4578 was filed at 16:10:52 and claimed by L5 at 16:18:14, then requeued at 16:21:45 to wait on BL-4546 (pushed 16:21:14). L4 reclaimed it at 16:22:27 and closed it at 16:33:26. A second failure, Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls (run 41541574110), was filed as BL-4579 at 16:15:42 and closed at 16:24:11 as already handled. Green came with run 41546343806 at 23:51:56Z.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261102-172448\logs -CiRunsJson Z:\repos\Curl.audit\20261102-172448\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261102-172448\logs\DarkFactory-20261102-095114.log -Pattern '41537057745|BL-4578'
```

- Expected: No red spell over 30 minutes
- Actual: runId 41537057745, start 2026-11-02T22:59:50Z, end 2026-11-02T23:51:56Z, minutes 52.1; Quality gates (windows-latest) failure

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 87cd20bd 9742157d.
