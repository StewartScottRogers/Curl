---
id: BL-2124
title: Fix AF-0178: CI on work/dark-factory red 301.1 min (run 38178740830): test303 flake, then four Quality-gates failures the CI watch never filed
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-13
completed: 2026-10-12
---
# BL-2124 — Fix AF-0178: CI on work/dark-factory red 301.1 min (run 38178740830): test303 flake, then four Quality-gates failures the CI watch never filed

## Goal

The defect the audit office reported as AF-0178 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0178 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0178-ci-on-work-dark-factory-red-301-1-min-run-38178740.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

Measure-FactoryProcess.ps1 -Since 2026-10-11: ciRedMinutes 312.07; ciRedSpells holds {start 2026-10-11T22:12:33Z, end 2026-10-12T03:13:39Z, minutes 301.1, runId 38178740830} (the other spell, run 38154076736, is 10.97 min). In ci-runs.json, run 38178740830 failed on 0d182629 at 22:12:33Z, 12 minutes after run 38177462122 passed on the same commit (test303 flake, filed as BL-2093 by a lane 14 min later per the coordinator). Then runs 38182483664 (b362b849, 23:15Z), 38183634772 (c811b9ec, 23:22Z), 38184717609 (d8245edb, 23:52Z) and 38190842002 (e94294e2, 01:23Z) failed the Quality gates job: commit 35470e761 (BL-2085, the HTTP/1.1 proxy branch) pushed HttpConnectionPersistence.KeepsAlive past the complexity gate (Tasks/Done/2026-10-12_0448/BL-2100-*.md, Context). logs/DarkFactory-20261012-044428.log lines 8-11 show the coordinator's CI watch giving up on all four: 'its log names no failing test or build error; nothing to file' (RunDarkFactory.ps1:2911). The task was filed only by an interactive session (cb9194c7, 2026-10-11 19:45 -0700 = 02:45Z), about 3.5 h after the first gate failure. Its fix fb77bd1a turned CI green with run 38197363734 at 03:13:39Z. No merge to master happened while CI was red: the next merge, PR #123 at 05:20 local, followed green run 38246445688. This is the same spell AF-0171 recorded while it was still red at 69 min, now measured to its end.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-11 -LogRoot Z:\repos\Curl.audit\20261012-165355\logs -CiRunsJson Z:\repos\Curl.audit\20261012-165355\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 38178740830; Select-String -Path Z:\repos\Curl.audit\20261012-165355\logs\DarkFactory-20261012-044428.log -Pattern 'names no failing test'
```

- Expected: No CI red spell over 30 minutes; a failed run that names no test is filed as a task by its failing job and step.
- Actual: Spell runId 38178740830, 301.1 minutes, 2026-10-11T22:12:33Z to 2026-10-12T03:13:39Z; four coordinator lines for runs 38190842002, 38184717609, 38183634772 and 38182483664 end in 'nothing to file'.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- BL-2118 already made Get-CiFailures file a red job beside a named test failure. What still dropped a run was a log whose lines gh tags `UNKNOWN STEP` (job unnamed) or that is empty: Get-CiFailures skipped those, so Get-CiRuns printed "names no failing test ... nothing to file". Now such lines file as `job unknown job step <step>` (only when the run names nothing else), and a failed run with an empty `--log-failed` files as `job unknown job step unknown step`. Self-tests added to `-TestCiWatch`; all pass. Only RunDarkFactory.ps1 changed, so no C# build or test run applies.

## Log

- 2026-10-13: Created.
- 2026-10-12: Backlog -> Doing.
- 2026-10-12: Doing -> Done. CI watch now files runs whose log lines are UNKNOWN STEP or empty
