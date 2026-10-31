---
id: AF-0476
title: CI red 36 min from run 40441067741 (79c9d998, RunAsync_OutputInUtf8ConfigFileOnWindows on Linux and macOS)
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:40441067741:ci-red
reproduction: none
task: BL-4229
tasks: BL-4229
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0476 - CI red 36 min from run 40441067741 (79c9d998, RunAsync_OutputInUtf8ConfigFileOnWindows on Linux and macOS)

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 36 min from run 40441067741 (79c9d998, RunAsync_OutputInUtf8ConfigFileOnWindows on Linux and macOS).

## Evidence

Location: `logs/ci-runs.json`

Spell 2026-10-27T03:06:01Z to 03:41:50Z (20:06-20:41 Arizona), 35.82 min. Run 40441067741 failed on 79c9d998 (fix(http): drop HTTP/2 response fields with invalid names, CGR-137) with RunAsync_OutputInUtf8ConfigFileOnWindows_CreatesTheFileBytesReadInTheAnsiCodePage (DarkFactory-20261026-151632.log:136). The cause was a Windows-only test that was not marked for Windows. BL-3911 was claimed at 20:23:29 (DarkFactory-20261026-151632-L8.log:196) and Done the same evening.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-25 -LogRoot Z:/repos/Curl.audit/20261029-060658/logs -CiRunsJson Z:/repos/Curl.audit/20261029-060658/logs/ci-runs.json -OutFile $env:TEMP/process.json; (Get-Content $env:TEMP/process.json -Raw | ConvertFrom-Json).ciRedSpells | ? runId -eq 40441067741
```

- Expected: No red spell over 30 minutes.
- Actual: minutes 35.82, runId 40441067741.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: yes | A Linux and macOS test regression again turned CI red for over 30 min. Run 40984137987 (4c618d0f) was red 138.85 min; BL-4299 (Build_RelativeRealm_ReturnsNull on Linux+macOS) was filed at 18:50:48, claimed 19:31:03 and Done 19:48:21.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
