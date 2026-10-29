---
id: BL-4236
title: Fix AF-0483: BL-3617 cost $6.38 (5.9x median): its first opus run hit the 80-turn cap, the second took 68 turns
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, DarkFactory/Prompts.ps1]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4236 — Fix AF-0483: BL-3617 cost $6.38 (5.9x median): its first opus run hit the 80-turn cap, the second took 68 turns

## Goal

What drove the past run's cost that the audit office reported as AF-0483 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0483 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0483-bl-3617-cost-6-38-5-9x-median-its-first-opus-run-h.md`.

Location: `logs/BL-3617-20261026-063535-L5.jsonl`

Location: `logs/BL-3617-20261026-063535-L5.jsonl`

Run L5 on 2026-10-26, 13:03:08 to 13:32:11: 81 turns in 29 min, requeued at 13:36:17 on the 80-turn cap (DarkFactory-20261026-063535-L5.log:308). The retry on L1, 13:40:44 to 14:41:17, took 68 turns and 60.5 min and finished Done. The capped run's work was redone by the retry.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-3617'
```

- Expected: No task over 3x the median costUsd and over 5 USD.
- Actual: BL-3617 costUsd 6.3804, claims 2, outcome done.

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes
- Cause, from both logs (Z:/repos/Curl.audit/20261029-060658/logs/BL-3617-20261026-063535-L5.jsonl and -L1.jsonl): BL-3617 was one gap finding (CGR-088) spanning three libraries (FTP, HTTP, Networking/SOCKS) and six failure kinds, each measured against real curl. The L5 run (81 turns, about $3.56) did no sizing pass, started editing at tool call 32, and reached the 80-turn cap with no warning that it existed; the L1 retry (68 turns, $2.82) applied L5's stash, split the remainder off as BL-3880, fixed two complexity-gate methods and finished. The finding's "capped run's work was redone" does not hold: L1 built on the stash and did not re-measure curl.
- Already fixed: the unannounced turn cap is the cause AF-0482 named, and BL-4235 (fd612e55e) now tells every run its cap and asks it to hand back 15 turns before it. The sizing pass is already rule 4 of the lane prompt.
- Avoidable waste fixed here: L5 spent tool calls 8 to 16 (9 of 81) finding the built Curl to give Record-CurlExchange.ps1 -Curl, because listing Curl.Console\bin is denied to a lane and no rule named the path; it ended up grepping Done tasks for it. Both lane prompts in DarkFactory/Prompts.ps1 now name `Curl.Console\bin\Debug\net10.0\curl.exe` (the AssemblyName is `curl`) under rule 0 and say not to look for it.
- Touches widened to DarkFactory/Prompts.ps1, where the lane prompt text lives; no task in Doing on origin/work/dark-factory names it (checked BL-3927, BL-3933, BL-4205, BL-4222, BL-4242, BL-4246, BL-4250, BL-4253). RunDarkFactory.ps1 needed no change.

- `dotnet build`: 0 warnings, 0 errors; fast tests: 64 test projects passed, none failed. No library production code changed, so no quality run was needed.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-10-29: Doing -> Done. Lane prompts name the built curl.exe for Record-CurlExchange -Curl; BL-3617's cap overrun is the cause BL-4235 fixed
