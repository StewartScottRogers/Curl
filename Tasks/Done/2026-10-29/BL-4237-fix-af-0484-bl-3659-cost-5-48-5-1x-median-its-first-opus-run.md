---
id: BL-4237
title: Fix AF-0484: BL-3659 cost $5.48 (5.1x median): its first opus run hit the 80-turn cap
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4237 — Fix AF-0484: BL-3659 cost $5.48 (5.1x median): its first opus run hit the 80-turn cap

## Goal

What drove the past run's cost that the audit office reported as AF-0484 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0484 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0484-bl-3659-cost-5-48-5-1x-median-its-first-opus-run-h.md`.

Location: `logs/BL-3659-20261026-151632-L4.jsonl`

Location: `logs/BL-3659-20261026-151632-L4.jsonl`

Run L4 on 2026-10-26, 18:15:17 to 18:52:39: 81 turns in 37.3 min, requeued on the 80-turn cap (DarkFactory-20261026-151632-L4.log:148). The retry on L6, 2026-10-28 06:07:53 to 06:35:54, took 46 turns and finished Done. The cap was the driver.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3659'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3659 costUsd 5.4816, claims 2, outcome done.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-29 (lane 8): Read both runs' tool calls (`Z:\repos\Curl.logs\BL-3659-20261026-151632-L4.jsonl`, `...-20261028-052757-L6.jsonl`).
  - Cause: L4 ($3.91, 81 turns, 99 tool calls) was a real feature (SSH connection reuse across ten files, plus measuring real curl against an sshd). It was not told about the 80-turn cap, so it kept working and was stopped while fixing its last two failing Cgr130 tests (calls 90 to 99), with no Notes written. L6 ($1.57, 46 turns) applied L4's stash and finished it, so no code was lost. The driver was the cap stopping a nearly finished run with no warning.
  - Fixed already: BL-4235 (fd612e55e, AF-0482) makes every run's prompt name its turn cap and ask it to hand back what is left, with what it measured under Notes, 15 turns before the cap. With that in place, L4 would have handed back at turn 65 with its sshd setup recorded.
  - Avoidable waste inside L4 that `RunDarkFactory.ps1` cannot fix: about 16 calls (14 to 29) finding an old WSL sshd and building Curl.Console into a scratch folder (four failed builds), 9 offset reads of `SshProtocolHandler.cs` after it had already been read whole, and 8 calls searching for `NumConnects`. Rule 10 of the lane prompt already forbids the slice reads; the rest is ordinary exploration. L6 then rebuilt the sshd setup from scratch (calls 9 to 20) because L4 had recorded nothing; the BL-4235 hand-back now records it.
  - Decision: no change to `RunDarkFactory.ps1`; its help (lines 90 to 93) already describes the cap and the AF-0482 hand-back. The cap's requeue reason says "split it" (DarkFactory/ShiftRunner.ps1, outside touches); L6 ignored that, which did no harm, and resumed the stash correctly.
- Build clean (0 warnings) and fast tests run 2026-10-29 in lane 8.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-29: Doing -> Done. AF-0484's driver (BL-3659's L4 run stopped at the 80-turn cap without warning) is named; BL-4235 already fixes it by telling runs their cap and asking for a hand-back 15 turns before it
