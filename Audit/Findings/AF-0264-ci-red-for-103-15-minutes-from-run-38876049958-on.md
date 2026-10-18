---
id: AF-0264
title: CI red for 103.15 minutes from run 38876049958 on 241c183e (Quality gates); fix done in 16.7 min, then 5 cancelled runs
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:38876049958:ci-red
reproduction: none
task: BL-2775
tasks: BL-2775
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0264 - CI red for 103.15 minutes from run 38876049958 on 241c183e (Quality gates); fix done in 16.7 min, then 5 cancelled runs

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red for 103.15 minutes from run 38876049958 on 241c183e (Quality gates); fix done in 16.7 min, then 5 cancelled runs.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-16T09:02:13Z to 10:45:22Z, 103.15 min, run 38876049958, stillRed False. Runs in ci-runs.json: 38876049958 failure (241c183e), 38878261240 cancelled, 38880310277 failure (0c71f7fb), 38882357911, 38884026379, 38884492897 and 38886128544 cancelled, 38887776973 success at 10:45:22Z. This is the same incident as AF-0244 (Quality gates job, Libcurl CurlMulti.SetOption branch gap, BL-2466/BL-2467). The window starting 2026-10-16 includes it again; the coordinator log that recorded it is in the previous audit's copy (DarkFactory-20261015-200601.log). Reported because the rule crosses; it should close as a duplicate of AF-0244.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table
```

- Expected: No red spell over 30 minutes.
- Actual: 2026-10-16T09:02:13Z 2026-10-16T10:45:22Z 103.15 38876049958 False

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
