---
id: AF-0731
title: CI red 74 minutes from run 43110153266 (27be8cbc); no filing trace, next green 74 min after
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:43110153266:ci-red
reproduction: none
task: BL-5090
tasks: BL-5090
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0731 - CI red 74 minutes from run 43110153266 (27be8cbc); no filing trace, next green 74 min after

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 74 minutes from run 43110153266 (27be8cbc); no filing trace, next green 74 min after.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: 2026-11-11T22:34:57Z to 23:48:57Z, 74 min, runId 43110153266. Failed runs 43112494560, 43114715030, 43115791546, 43117708980 inside the spell; no coordinator ci line in the copied logs names any of them; green run 43120906259 ended 23:48:57Z.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-11 -LogRoot <logs> -CiRunsJson <logs>/ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object minutes -gt 30
```

- Expected: Every failed run filed or green within 30 minutes of its end.
- Actual: Spell runId 43110153266, 74 minutes

## Re-audits

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
