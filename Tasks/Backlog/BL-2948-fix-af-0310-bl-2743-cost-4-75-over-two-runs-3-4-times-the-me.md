---
id: BL-2948
title: Fix AF-0310: BL-2743 cost $4.75 over two runs, 3.4 times the median, after a requeue that stashed finished engine work
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-21
completed:
---
# BL-2948 — Fix AF-0310: BL-2743 cost $4.75 over two runs, 3.4 times the median, after a requeue that stashed finished engine work

## Goal

What drove the past run's cost that the audit office reported as AF-0310 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0310 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0310-bl-2743-cost-4-75-over-two-runs-3-4-times-the-medi.md`.

Location: `logs/BL-2743-20261018-192454-L1.jsonl`

Location: `logs/BL-2743-20261018-192454-L1.jsonl`

Run 1 (BL-2743-20261018-192454-L1.jsonl): $2.96, 41 turns, 10.9 min, 5 builds, then requeued at 21:52:02 with 'Engine fix done and stashed; left: ...' (stash 3b7b8a47). Run 2 (BL-2743-20261019-061607-L3.jsonl): $1.78, 26 turns, 19.6 min. It applied stash 3b7b8a47 and re-ran the full build and test (33813/33813, 3573/3573). It got 'quality FAIL' at 06:29:15 and ran quality 3 more times before DONE at 06:38:11. What drove the cost: about 38% of it came from the second session rebuilding context and re-verifying stashed work. Stash 3b7b8a47 is still in git stash list after being applied.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2743'
```

- Expected: costUsd at most 4.2465 (3 times the median).
- Actual: BL-2743 claims 2, minutes 21.18, costUsd 4.7472, done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
