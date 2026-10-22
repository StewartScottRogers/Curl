---
id: AF-0331
title: CI red 46.0 min from run 39718635967 (Windows quality gate after BL-3022). Its fix task BL-3026 was filed with empty touches, so no lane would claim it, and lane 1 idled about 230 min on it
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-22_1120.md: the reproduction no longer reproduces.
key: process:logs:39718635967:ci-red
reproduction: none
task: BL-3084
tasks: BL-3084
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed: 2026-10-22
closed-how: reliable-reaudit
closed-by: 2026-10-22_1120.md
---
# AF-0331 - CI red 46.0 min from run 39718635967 (Windows quality gate after BL-3022). Its fix task BL-3026 was filed with empty touches, so no lane would claim it, and lane 1 idled about 230 min on it

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261021-203534-L2.log:84`: CI red 46.0 min from run 39718635967 (Windows quality gate after BL-3022). Its fix task BL-3026 was filed with empty touches, so no lane would claim it, and lane 1 idled about 230 min on it.

## Evidence

Location: `logs/DarkFactory-20261021-203534-L2.log:84`

ciRedSpells: start 2026-10-22T04:12:26Z, end 04:58:27Z, 46.02 min, runId 39718635967. First failing commit d6d35bf61 ('chore(tasks): complete BL-3022'). The gate failure was Unit2413ConnectionPeer.Mismatch outside a gate. The coordinator filed BL-3026 at 21:19:14. At 21:40:26 L2 logged 'BL-3026 unready not claimed: its touches are empty or *, so it would run alone and idle every other lane'. DarkFactory-20261021-203534-L1.log then has 198 'only unready tasks are ready (BL-3026)' wait lines, from 21:40:40 to 01:29:25, and the shift ended '(nothing ready)' at 01:30:31. CI turned green through the BL-3021 splits (success run 39723672567 on 1afd80a99), not through BL-3026. BL-3026 was closed by commit 167e765b2 as 'already fixed'. The CI watch filed a High CI-fix task with no touches, which made it unclaimable by lanes.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 39718635967; (Select-String -Path Z:\repos\Curl.audit\20261022-013026\logs\DarkFactory-20261021-203534-L*.log -Pattern 'BL-3026').Count
```

- Expected: No red spell over 30 minutes, and a CI-fix task that a lane can claim.
- Actual: minutes 46.02; 203 BL-3026 lines, 202 of them 'unready' waits or refusals.

## Re-audits

- 2026-10-22 | 2026-10-22_1120.md | reproduces: no | Historical spell from run 39718635967 is still 46.02 min (ended 04:58:27Z, before the window); the old log still has 203 BL-3026 lines. In this window, ciRedMinutes = 0. The incident's idle tail still shows: DarkFactory-20261021-203534-L1.log has 77 'only unready tasks are ready (BL-3026)' waits from 00:00:58 to 01:29:25 local, counted in waitOtherMinutes 77. BL-3026 now has touches and is Done (CI 39723672567 green), and BL-3028 changed the CI watch to file real touches, so no new empty-touches fix task appears.

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-22: accepted -> closed. Re-audit 2026-10-22_1120.md: the reproduction no longer reproduces.
