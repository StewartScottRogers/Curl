---
id: AF-0480
title: CI red 58 min from run 40801399142 (fcc2b504, Quality gates windows-latest: Curl.Core and Curl.Engine outside the gate)
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:40801399142:ci-red
reproduction: none
task: BL-4233
tasks: BL-4233
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0480 - CI red 58 min from run 40801399142 (fcc2b504, Quality gates windows-latest: Curl.Core and Curl.Engine outside the gate)

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261028-172341.log:65`: CI red 58 min from run 40801399142 (fcc2b504, Quality gates windows-latest: Curl.Core and Curl.Engine outside the gate).

## Evidence

Location: `logs/DarkFactory-20261028-172341.log:65`

Spell 2026-10-29T01:56:20Z to 02:54:15Z (18:56-19:54 Arizona), 57.92 min, on fcc2b504 (BL-4006's test change). BL-4017 was filed at 19:03:57 (DarkFactory-20261028-172341.log:65). It added two tests for Curl.Core and Curl.Engine coverage. Lanes do not run Measure-CodeQuality for every library their change affects, so coverage gaps first show in CI.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40801399142
```

- Expected: No red spell over 30 minutes.
- Actual: minutes 57.92, runId 40801399142.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
