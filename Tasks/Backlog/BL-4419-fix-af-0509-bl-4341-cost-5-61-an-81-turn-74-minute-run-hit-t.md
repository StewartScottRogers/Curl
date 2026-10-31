---
id: BL-4419
title: Fix AF-0509: BL-4341 cost $5.61: an 81-turn, 74-minute run hit the turn cap at $4.93 before a 21-turn rerun finished it
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-31
completed:
---
# BL-4419 — Fix AF-0509: BL-4341 cost $5.61: an 81-turn, 74-minute run hit the turn cap at $4.93 before a 21-turn rerun finished it

## Goal

What drove the past run's cost that the audit office reported as AF-0509 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0509 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0509-bl-4341-cost-5-61-an-81-turn-74-minute-run-hit-the.md`.

Location: `logs/BL-4341-20261029-184521-L1.jsonl`

Location: `logs/BL-4341-20261029-184521-L1.jsonl`

BL-4341-20261029-184521-L1.jsonl took 81 turns, $4.93, 74 min and 9.8 million cache-read tokens, and was requeued at 00:55:01 ('Dark factory stopped at its 80-turn cap'). BL-4341-20261029-184521-L13.jsonl then finished in 21 turns and $0.68 (Done 01:10:18). The pattern matches BL-4342 and BL-4112: the capped run's rereading drove the cost, and a short rerun did the rest.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-4341'
```

- Expected: costUsd under $5 or under 3 times the median.
- Actual: BL-4341 claims 2, costUsd 5.6059, outcome done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
