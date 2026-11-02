---
id: BL-3459
title: Fix AF-0412: BL-3418 cost $2.91 (6.6x the median) in one 49-turn Opus feature run porting checksrc.pl lines 900-1268
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, DarkFactory/ModelChoice.ps1]
requirement: none
created: 2026-10-24
completed: 2026-11-02
---
# BL-3459 — Fix AF-0412: BL-3418 cost $2.91 (6.6x the median) in one 49-turn Opus feature run porting checksrc.pl lines 900-1268

## Goal

What drove the past run's cost that the audit office reported as AF-0412 is found, and any avoidable waste in it is fixed, or the task records that there is none.

## Context

Filed from audit finding AF-0412 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0412-bl-3418-cost-2-91-6-6x-the-median-in-one-49-turn-o.md`.

Location: `logs/BL-3418-20261023-110315-L3.jsonl`

Location: `logs/BL-3418-20261023-110315-L3.jsonl`

One run: opus feature pipeline, 49 turns, 14.9 min, $2.90, 5.02M cache-read tokens, 40.9k output tokens. One failing test check along the way ('test 132/134 pass' at 15:54:40). Done at 16:03:52. Cost driver: Opus rereading a large, growing context for 49 turns on a port of upstream script lines, the kind of routine port CLAUDE.md says goes to sonnet.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261023-232221\logs\DarkFactory-20261023-110315-L3.log -Pattern 'BL-3418 (claim|test|end|DONE)'
```

- Expected: no task over 3 times the median costUsd
- Actual: BL-3418 costUsd 2.905; claim line shows '[model opus: feature pipeline]', end '49 turns, 14.9 min'

The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none.

## Acceptance criteria

- [x] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes
n- Cause: BL-3418 ("Port checksrc.pl scanfile line checks...") is a routine upstream-script port but its title matched no `$RoutineFeatureTitles` rule, so the feature pipeline sent it to opus (5.02M cache-read tokens over 49 turns). Avoidable: added the rule `upstream checksrc.pl port` (`^Port checksrc.plb`) to DarkFactory/ModelChoice.ps1 (its logic lives there, so it was added to touches; no other Doing task touches it) with a -TestModelChoice case; such ports now run on sonnet. Hard-work markers still override. Build/fast tests unaffected (PowerShell-only change); -TestModelChoice passes.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Deferred. Stewart rejected its finding on 2026-10-24 (single run under the new 5-dollar cost-outlier floor); no work wanted.
- 2026-11-02: Deferred -> Backlog. Stewart 2026-11-02: complete all existing tasks before the third-party handoff.
- 2026-11-02: Backlog -> Doing. Criteria at claim: af4de8c2 9742157d.
- 2026-11-02: Doing -> Done. Cause named; checksrc.pl ports now routed to sonnet
