---
id: BL-2779
title: Fix AF-0268: BL-2550 cost $7.39, 3.04 times the median: 24 of its 51.6 minutes went on two shell loops reading 2,077 upstream test files one by one
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-18
completed:
---
# BL-2779 — Fix AF-0268: BL-2550 cost $7.39, 3.04 times the median: 24 of its 51.6 minutes went on two shell loops reading 2,077 upstream test files one by one

## Goal

What drove the past run's cost that the audit office reported as AF-0268 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0268 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0268-bl-2550-cost-7-39-3-04-times-the-median-24-of-its.md`.

Location: `logs/DarkFactory-20261017-165834-L1.log:175`

Location: `logs/DarkFactory-20261017-165834-L1.log:175`

Measure-FactoryProcess.ps1: BL-2550 claims 1, 52.98 min, $7.3905 (median $2.4326). DarkFactory-20261017-165834-L1.log:127 claims it at 18:06:33; its first file write is at 18:35:37; it ended at 18:58:14 with '120 turns, 51.6 min' (line 175). Peer port batches BL-2549 and BL-2551 took 26.6 and 22.6 min. In BL-2550-20261017-165834-L1.jsonl, two Bash calls looped 'for f in *.curltest' over Curl.Conformance.UnitTests/UpstreamTestData (2,077 files), running grep/tr/awk per file. They ran 01:07:25Z to 01:17:42Z and 01:17:55Z to 01:31:54Z, about 24 minutes with the session held open. A single Select-String or grep -l over the folder does the same lookup in seconds. The run ended with a quality FAIL ('unexpected EOF' in a bash eval) and an empty stash 0e12a7c4 (no tracked or untracked changes), so no work was lost.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2550'; Select-String -Path Z:\repos\Curl.audit\20261018-104525\logs\DarkFactory-20261017-165834-L1.log -Pattern 'BL-2550 (claim|write|end)' | Select-Object -First 3
```

- Expected: BL-2550 under 3 times the median cost ($7.30).
- Actual: BL-2550 1 52.98 7.3905 done; claim 18:06:33, first write 18:35:37.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
