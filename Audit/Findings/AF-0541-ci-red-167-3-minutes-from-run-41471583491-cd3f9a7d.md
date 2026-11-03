---
id: AF-0541
title: CI red 167.3 minutes from run 41471583491 (cd3f9a7d); 104 minutes of it came after the fix landed, because newer pushes cancelled every run
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:41471583491:ci-red
reproduction: none
task: BL-4647
tasks: BL-4647
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0541 - CI red 167.3 minutes from run 41471583491 (cd3f9a7d); 104 minutes of it came after the fix landed, because newer pushes cancelled every run

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 167.3 minutes from run 41471583491 (cd3f9a7d); 104 minutes of it came after the fix landed, because newer pushes cancelled every run.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-11-02T16:19:10Z (09:19 Arizona), end 19:06:28Z (12:06), 167.3 min, runId 41471583491 on cd3f9a7d7 ('chore(tasks): split BL-4501...'). The coordinator could not read that run's failure log (DarkFactory-20261102-083146.log:23 at 09:21:19 and DarkFactory-20261102-095114.log at 09:57:47: 'cannot read its log with gh run view --log-failed'), so nothing was filed from it. The first readable failure was run 41479689790 on e9a9c5b9: ExecuteAsync_NtlmWithServiceNameAndNoCredentialsOffWindows_SendsNoAuthoriz..., which was added by 2d9fd3e51 (08:47, BL-4509, 'start NTLM for --service-name without -u on Windows only'). It passed on the Windows-only lane test and failed off Windows. BL-4513 was filed at 10:16:13 (57 min into the spell), claimed by L1 at 10:16:46 and pushed at 10:22:30. Run 41489970818 (13392545, started 10:18, before the fix) also failed. Then about 60 consecutive runs from 17:21Z to 18:43Z were cancelled by newer pushes, and none completed until 41503280775 (ef6501d3) went green at 19:06Z. So about 104 of the 167 minutes passed after the fix was on the branch, because lanes pushed every 2-3 minutes and kept cancelling the run.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-02 -LogRoot Z:\repos\Curl.audit\20261102-172448\logs -CiRunsJson Z:\repos\Curl.audit\20261102-172448\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object minutes -gt 30
```

- Expected: No red spell over 30 minutes
- Actual: runId 41471583491, start 2026-11-02T16:19:10Z, end 2026-11-02T19:06:28Z, minutes 167.3

## Re-audits

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
