---
id: BL-4648
title: Fix AF-0542: CI red 56.5 minutes from run 41516637939 (6ada3366): MQTT 5 CONNECT and Frame_Malformed conformance tests
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
# BL-4648 — Fix AF-0542: CI red 56.5 minutes from run 41516637939 (6ada3366): MQTT 5 CONNECT and Frame_Malformed conformance tests

## Goal

The cause of the factory behaviour the audit office reported as AF-0542 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0542 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0542-ci-red-56-5-minutes-from-run-41516637939-6ada3366.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-11-02T20:34:49Z (13:34 Arizona), end 21:31:19Z (14:31), 56.5 min, runId 41516637939 on 6ada3366a. Failing test: Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst, from the MQTT 5 reference broker added in 6007992eb (12:41). The coordinator filed BL-4543 at 13:28:52 from the failed jobs of the run while it was still going (DarkFactory-20261102-095114.log:110). L6 fixed it and pushed at 13:51:53. Duplicate fix tasks were also filed for the same test: BL-4544 (renumbered from a second BL-4543, L5, done 13:53:27) and BL-4547 (L3 claimed it at 13:47:08 and requeued it at 13:53:58 to wait on BL-4543; L1 reclaimed it and closed it at 14:10:19 as 'fixes confirmed'), so three fix runs covered one test. A second test, Frame_Malformed_IsAnsweredWithConnectionClose501 (run 41519828305), was filed as BL-4548 at 13:47:51 and closed at 14:11:17 as 'Already fixed by e6e55c175'. Run 41521714156 (e6e55c17) failed with an unreadable log. Green came with run 41526034389 at 21:31Z, 40 minutes after the first fix landed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261102-172448\logs -CiRunsJson Z:\repos\Curl.audit\20261102-172448\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261102-172448\logs\DarkFactory-20261102-095114*.log -Pattern 'BL-454[3478] (ci|claim|REQUEUE|DONE)'
```

- Expected: No red spell over 30 minutes; one fix task per failing test
- Actual: runId 41516637939, 56.5 minutes; BL-4543, BL-4544 and BL-4547 all claimed for the same MQTT 5 CONNECT test

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
