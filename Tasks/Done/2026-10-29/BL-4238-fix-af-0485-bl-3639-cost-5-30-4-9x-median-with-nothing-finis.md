---
id: BL-4238
title: Fix AF-0485: BL-3639 cost $5.30 (4.9x median) with nothing finished: a requeue on held files, then 81 turns in 14 min at the cap
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4238 — Fix AF-0485: BL-3639 cost $5.30 (4.9x median) with nothing finished: a requeue on held files, then 81 turns in 14 min at the cap

## Goal

What drove the past run's cost that the audit office reported as AF-0485 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0485 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0485-bl-3639-cost-5-30-4-9x-median-with-nothing-finishe.md`.

Location: `logs/BL-3639-20261028-052757-L9.jsonl`

Location: `logs/BL-3639-20261028-052757-L9.jsonl`

Run L6 on 2026-10-26: 27 turns, 14.4 min, requeued waiting on files held by BL-3570 and BL-3893 (DarkFactory-20261026-151632-L6.log:104). Run L9 on 2026-10-28, 06:06:02 to 06:20:14: 81 turns in 14.2 min, stopped at the 80-turn cap (DarkFactory-20261028-052757-L9.log:20). The task was then Deferred and split. All $5.30 was spent with no task Done.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3639'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3639 costUsd 5.304, claims 2, outcome requeued.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause, from `BL-3639-20261026-151632-L6.jsonl`, `BL-3639-20261028-052757-L9.jsonl` and the two shift logs in the audit copy:
  - Run L6 (27 turns, about $0.97): it measured real curl, wrote a plan, and found that the plan needed `CommandLineOptionTable.cs` (held by BL-3570) and `CommandLineRefusal.cs` (held by BL-3893). The task's `touches` named only the new test file, so the shift could not have seen the clash before the claim. The run requeued before editing any code and recorded its measurements and plan under Notes. The shift waived the run ("no work to keep", DarkFactory-20261026-151632-L6.log:102), so it did not count toward the cap. This part is unavoidable: the clash can only be found by reading the code.
  - Run L9 (81 turns, $4.33, `error_max_turns`): the plan changed five libraries (Curl.Core, Curl.Cli, Curl.Engine, Curl.Console, Curl.Protocol.Http). Turns 44 to 87 went on the `-F f=@-` abort-timing case: debugging HttpRequestBodyWriter flushing and socket linger against a loopback recording. The run did not know it had an 80-turn cap, so it was still mid-debugging when the cap stopped it and handed back nothing, not even what was left to do. This is the avoidable waste. It is the same cause as AF-0482, and BL-4235 (fd612e55e) has already fixed it: RunDarkFactory.ps1 and DarkFactory/Prompts.ps1 now name the cap in the task and lane prompts, and ask the run to hand back what is left 15 turns before it. With that, run L9 would have filed the `-F` timing work and requeued at about turn 65. That is the split the next claim made anyway (BL-3972, BL-3973, BL-3974).
- Decision: no further change to RunDarkFactory.ps1. The avoidable cause is already fixed by BL-4235, and the L6 requeue cost nothing toward the cap.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-29: Doing -> Done. AF-0485 cause named: L6 requeue was waived and unavoidable; L9's turn-cap stop is fixed by BL-4235 telling runs their cap
