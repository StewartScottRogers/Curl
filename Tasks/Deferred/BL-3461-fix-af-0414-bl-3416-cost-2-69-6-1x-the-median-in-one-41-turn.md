---
id: BL-3461
title: Fix AF-0414: BL-3416 cost $2.69 (6.1x the median) in one 41-turn Opus run finishing the checksrc.pl entry point
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-24
completed:
---
# BL-3461 — Fix AF-0414: BL-3416 cost $2.69 (6.1x the median) in one 41-turn Opus run finishing the checksrc.pl entry point

## Goal

What drove the past run's cost that the audit office reported as AF-0414 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0414 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0414-bl-3416-cost-2-69-6-1x-the-median-in-one-41-turn-o.md`.

Location: `logs/BL-3416-20261023-110315-L3.jsonl`

Location: `logs/BL-3416-20261023-110315-L3.jsonl`

One run: opus feature pipeline, 41 turns, 16.9 min, $2.69, 3.69M cache-read tokens, 47.2k output tokens. It started by applying an earlier run's stash c1205b3f at 15:11:42. Done at 15:30:50. Cost driver: Opus output-heavy porting work (47k output tokens) on a stashed continuation.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-20261023-110315-L3.log -Pattern 'BL-3416 (claim|stash|end|DONE)'
```

- Expected: no task over 3 times the median costUsd
- Actual: BL-3416 costUsd 2.6877; '41 turns, 16.9 min [model opus: feature pipeline]'

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Deferred. Stewart rejected its finding on 2026-10-24 (single run under the new 5-dollar cost-outlier floor); no work wanted.
