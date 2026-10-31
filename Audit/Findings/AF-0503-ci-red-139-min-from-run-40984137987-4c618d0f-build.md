---
id: AF-0503
title: CI red 139 min from run 40984137987 (4c618d0f, Build_RelativeRealm_ReturnsNull on Linux and macOS, then the quality gates)
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:40984137987:ci-red
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0503 - CI red 139 min from run 40984137987 (4c618d0f, Build_RelativeRealm_ReturnsNull on Linux and macOS, then the quality gates)

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261029-184521.log`: CI red 139 min from run 40984137987 (4c618d0f, Build_RelativeRealm_ReturnsNull on Linux and macOS, then the quality gates).

## Evidence

Location: `logs/DarkFactory-20261029-184521.log`

ciRedSpells: 2026-10-30T01:30:16Z to 03:49:07Z, 138.85 min, runId 40984137987, headSha 4c618d0f (18:30-20:49 MST 10-29). At 18:50:48 BL-4299 was filed (regression Build_RelativeRealm_ReturnsNull on Linux+macOS). L8 did not claim it until 19:31:03, 40 min later, and it was Done at 19:48:21. A second failure, the Quality gates (windows-latest) job, was filed as BL-4308 at 19:58:08 and Done at 20:33:48. Green at 20:49 MST. Lanes test only on Windows, so a Linux/macOS test and a coverage gate both reached the shared branch.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-29 -LogRoot Z:/repos/Curl.audit/20261031-035829/logs -CiRunsJson Z:/repos/Curl.audit/20261031-035829/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40984137987
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-30T01:30:16Z end 2026-10-30T03:49:07Z minutes 138.85 runId 40984137987

## Re-audits

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
