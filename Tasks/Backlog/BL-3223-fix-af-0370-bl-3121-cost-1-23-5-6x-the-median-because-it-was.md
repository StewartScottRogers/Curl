---
id: BL-3223
title: Fix AF-0370: BL-3121 cost $1.23 (5.6x the median) because it was filed with touches naming only Curl.Networking.UnitLibrary, so its first Opus run found BL-3127 holding the test project and requeued
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-23
completed:
---
# BL-3223 — Fix AF-0370: BL-3121 cost $1.23 (5.6x the median) because it was filed with touches naming only Curl.Networking.UnitLibrary, so its first Opus run found BL-3127 holding the test project and requeued

## Goal

What drove the past run's cost that the audit office reported as AF-0370 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0370 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0370-bl-3121-cost-1-23-5-6x-the-median-because-it-was-f.md`.

Location: `logs/BL-3121-20261022-143901-L2.jsonl`

Location: `logs/BL-3121-20261022-143901-L2.jsonl`

Median task costUsd is 0.2207 (52 tasks), so the bar is 0.662. BL-3121 costUsd=1.2265, claims=2. Run 1 (L2, 14:39:44): 11 turns, $0.299, result: 'I moved BL-3121 back to Backlog without changing any code, because the file it needs is held by another lane.' Commit 8ea47c96c widened touches from [Curl.Networking.UnitLibrary] to [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]. BL-3127 (touches [Curl.Networking.UnitTests]) was claimed at 14:40:31 on L4. Run 2 (L1, 16:10:56, 'retry after a run that came back'): 28 turns, $0.927, 1.50M cache-read tokens. It included a refused Bash command ('Permission to use Bash with command cd /z/repos/C...') followed by mutating QuicDialer.cs by hand and reverting it. The fix was test-only, but routing chose Opus ('names HTTP/3').

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3121'
```

- Expected: costUsd at most 3 times the median (0.662).
- Actual: BL-3121 claims=2 minutes=6.15 costUsd=1.2265 outcome=done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
