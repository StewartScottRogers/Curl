---
id: BL-2950
title: Fix AF-0312: BL-2724 cost $4.45, 3.1 times the median, in one 73-turn run
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Prompts.ps1]
requirement: none
created: 2026-10-21
completed:
---
# BL-2950 — Fix AF-0312: BL-2724 cost $4.45, 3.1 times the median, in one 73-turn run

## Goal

What drove the past run's cost that the audit office reported as AF-0312 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0312 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0312-bl-2724-cost-4-45-3-1-times-the-median-in-one-73-t.md`.

Location: `logs/BL-2724-20261018-192454-L1.jsonl`

Location: `logs/BL-2724-20261018-192454-L1.jsonl`

One run: claim at 04:45:35, end at 05:08:14 (22.6 min), DONE at 05:09:35. 73 turns, 9.26M cache-read tokens, 43K output. Tools: 35 Bash (7 grep, 3 sed, 3 cat, many of them over the upstream curl sources it unpacked to /tmp/up2724: ftp.c, pingpong.c, ftpserver.pl), 13 Edit, 6 Grep, 4 PowerShell, and the same two test files read twice. What drove the cost: researching upstream's FTP response-timeout behaviour inside the run, with the context re-read every turn. No retries.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2724'
```

- Expected: costUsd at most 4.2465 (3 times the median).
- Actual: BL-2724 claims 1, minutes 24, costUsd 4.4497, done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
