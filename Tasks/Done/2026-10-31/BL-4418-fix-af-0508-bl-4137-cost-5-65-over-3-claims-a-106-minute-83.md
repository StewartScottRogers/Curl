---
id: BL-4418
title: Fix AF-0508: BL-4137 cost $5.65 over 3 claims; a 106-minute, 83-turn run ($3.91) ran out of time with its code stashed
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4418 — Fix AF-0508: BL-4137 cost $5.65 over 3 claims; a 106-minute, 83-turn run ($3.91) ran out of time with its code stashed

## Goal

What drove the past run's cost that the audit office reported as AF-0508 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0508 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0508-bl-4137-cost-5-65-over-3-claims-a-106-minute-83-tu.md`.

Location: `logs/BL-4137-20261030-060635-L6.jsonl`

Location: `logs/BL-4137-20261030-060635-L6.jsonl`

Runs: BL-4137-20261029-184521-L5.jsonl (21 turns, $0.75; split for size); BL-4137-20261030-060635-L6.jsonl (83 turns, $3.91, 106 min, 8.2 million cache-read tokens; result 'I ran out of time ... uncommitted, for the shift to stash'); BL-4137-20261030-060635-L4.jsonl (35 turns, $0.99; Done). The long run that could not commit drove the cost.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-4137'
```

- Expected: costUsd under $5 or under 3 times the median.
- Actual: BL-4137 claims 3, costUsd 5.6532, outcome done

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-31 (lane 3): Cause of AF-0508's cost: BL-4137 (client flow of the OHTTP extension) was claimed 3 times. Claim 1 ($0.75, 21 turns) split it for size, the correct first-pass behaviour. Claim 2 (lane 6, 83 turns, $3.91, 106 minutes, 8.2 million cache-read tokens) wrote all the code and 30-odd tests, then ran Measure-CodeQuality.ps1 with Composition added, which took 100 minutes and ended with dotnet test exit 1 although every project printed Passed; the deadline hit with the work uncommitted. Claim 3 (lane 4, 35 turns, $0.99) measured the one library alone in about a minute, fixed two members and finished. The avoidable part, a quality run scoped past the changed library, is already prevented by the lane rules (one Measure-CodeQuality.ps1 run per changed library) and by BL-4137's Notes. Nothing in RunDarkFactory.ps1 drove the cost (no rerun loop; it followed one long implementation run), so no change is made: recorded as unavoidable. The historical cost cannot be re-measured; the first audit after Done closes it.

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-31: Doing -> Done. Cause named and recorded as unavoidable; see Notes
