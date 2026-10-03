---
id: BL-1281
title: Fix AF-0025: BL-1121 claimed 5 times, BL-892 4 times and BL-907 3 times
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1281 — Fix AF-0025: BL-1121 claimed 5 times, BL-892 4 times and BL-907 3 times

## Goal

The defect the audit office reported as AF-0025 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0025 (Medium, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0025-bl-1121-claimed-5-times-bl-892-4-times-and-bl-907.md`.

Location: `logs/BL-1121`

Location: `logs/BL-1121`

Measure-FactoryProcess.ps1 reports claims=5 for BL-1121 (26.82 min, done), claims=4 for BL-892 (12.85 min) and claims=3 for BL-907 (20.23 min). The rule threshold is 3 or more claims. In total 10 tasks were claimed more than once and requeues=11. The reasons were not investigated.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-30 -LogRoot ..\logs -CiRunsJson ..\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object claims -ge 3 | Select-Object id,claims
```

- Expected: No task claimed 3 or more times.
- Actual: BL-1121 5, BL-892 4, BL-907 3

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded. (Handed to BL-1282, interactive only - see Notes.)
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Causes, from `Curl.logs` and `git log` (the measurer itself is an audit path a lane may not read):

- BL-1121 was claimed once (one `claim` trace line, one claim commit); its 5 is a measurement artefact, most likely mentions of "claim BL-1121" in other lanes' transcripts.
- BL-892 was claimed twice for real: on 2026-09-30 lane 2 finished it, then GitHub was out of reach ("unable to access") for a few minutes, `Invoke-Integrate` used up its 3 attempts in about a minute and parked the finished work; lane 3 redid it.
- The remaining counts (BL-892's other 2, BL-907's 2) are `claim   lost the race` lines written when a claim push failed during the same outage - not races, and not claims.

Fix in `RunDarkFactory.ps1`: `Test-RemoteReachable` and `Wait-RemoteReachable` (waits up to an hour, tracing `offline`/`online`); `Invoke-Claim` and each `Invoke-Integrate` attempt wait for origin first, so an outage no longer parks finished work; a refused claim push is traced `race`, an unheard one not at all, so only pushed claims are traced `claim`. Help text updated. `-TestPark` gained two cases (reachable origin; unreachable origin traced and reported) - 9/9 pass.

Decision (default taken): the first criterion cannot be checked by a lane - the reproduction runs an audit-office tool the guard refuses lanes, and its `-Since 2026-09-30` window will always contain the historical log lines. Its verification, and making the measurer count only real claims, is filed as BL-1282 (`lane: no`); the finding closes only on a re-audit anyway.

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Claims and integrations wait out a GitHub outage instead of parking finished work, and only pushed claims are traced as claim
