---
id: BL-3083
title: Fix AF-0330: CI red 48.4 min on work/dark-factory from run 39691487971: Windows quality gate (coverage, complexity, CRAP) failed after BL-2856's push
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-22
completed:
---
# BL-3083 — Fix AF-0330: CI red 48.4 min on work/dark-factory from run 39691487971: Windows quality gate (coverage, complexity, CRAP) failed after BL-2856's push

## Goal

The defect the audit office reported as AF-0330 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0330 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0330-ci-red-48-4-min-on-work-dark-factory-from-run-3969.md`.

Location: `logs/DarkFactory-20261021-150011.log`

Location: `logs/DarkFactory-20261021-150011.log`

ciRedSpells: start 2026-10-21T23:21:56Z, end 2026-10-22T00:10:17Z, 48.35 min, runId 39691487971. First failing commit 01deb2980 ('chore(tasks): complete BL-2856 and file BL-2996...'), job 'Quality gates (windows-latest) step Measure coverage, complexity and CRAP'. The coordinator saw it at 16:23:12 MST and filed BL-2998 at 16:29:31. L1 claimed it at 16:47:29 and pushed DONE at 16:56:44 ('WriteBytesAsync split'). Run 39696631298 then succeeded. About 18 min passed between filing and claim. The lane integrate step logs 'quality ok' but did not catch a member outside the gate that the CI quality job measures.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-19 -LogRoot Z:\repos\Curl.audit\20261022-013026\logs -CiRunsJson Z:\repos\Curl.audit\20261022-013026\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 39691487971
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-21T23:21:56Z, end 2026-10-22T00:10:17Z, minutes 48.35

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
