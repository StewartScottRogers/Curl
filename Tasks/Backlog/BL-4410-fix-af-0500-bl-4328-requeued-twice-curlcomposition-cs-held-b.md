---
id: BL-4410
title: Fix AF-0500: BL-4328 requeued twice (CurlComposition.cs held by BL-4320, then the 80-turn cap) and then Deferred and split
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-31
completed:
---
# BL-4410 — Fix AF-0500: BL-4328 requeued twice (CurlComposition.cs held by BL-4320, then the 80-turn cap) and then Deferred and split

## Goal

The cause of the factory behaviour the audit office reported as AF-0500 is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding.

## Context

Filed from audit finding AF-0500 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0500-bl-4328-requeued-twice-curlcomposition-cs-held-by.md`.

Location: `logs/DarkFactory-20261029-184521-L7.log`

Location: `logs/DarkFactory-20261029-184521-L7.log`

L14 claimed it at 22:06:44. That run took 22 turns and 4.7 min, and was requeued at 22:16:33 ('Needs Curl.Console/CurlComposition.cs, held by BL-4320 in Doing'). L7 claimed it at 22:25:11. That run took 81 turns and 50.5 min, and was requeued at 00:07:54 ('Dark factory stopped at its 80-turn cap'). L2 split it over the cost cap at 13:02:42 on 10-30 into BL-4362..BL-4365, and it was Deferred. The file conflict was found only after a claim and a run, not at claim time.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Z:/repos/Curl.audit/20261031-035829/logs/DarkFactory-*-L*.log -Pattern 'BL-4328 (claim|end|REQUEUE|split)'
```

- Expected: A held file is seen at claim time; a too-big task is split before an 80-turn run.
- Actual: 2 REQUEUE (held file; 80-turn cap), then split and Deferred.

The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's measurement, run over the live logs written after the fix landed (`-LogRoot` the repository's `.logs` folder, `-CiRunsJson` a fresh `gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500`, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
