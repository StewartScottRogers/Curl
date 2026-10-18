---
id: AF-0266
title: CI red for 62.28 minutes from run 39091460067 on 1c266d83 (BL-2551): ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne failed on Windows; fix done in 18 min, then 5 cancelled runs
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:39091460067:ci-red
reproduction: none
task: none
tasks:
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0266 - CI red for 62.28 minutes from run 39091460067 on 1c266d83 (BL-2551): ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne failed on Windows; fix done in 18 min, then 5 cancelled runs

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red for 62.28 minutes from run 39091460067 on 1c266d83 (BL-2551): ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne failed on Windows; fix done in 18 min, then 5 cancelled runs.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-18T03:04:00Z to 04:06:17Z, 62.28 min, run 39091460067. Commit 1c266d83 is BL-2551's 'port libtest programs 2505 to 3100'. The failing test was ListenAsync_WhenTheFirstPortIsTaken_ListensOnTheNextFreeOne on Windows: a port-collision race, where a bare NullReferenceException hit when the port was taken. DarkFactory-20261017-165834.log:32 saw the failure at 20:05:03 local; line 33 filed BL-2707 at 20:11:45. BL-2707 was claimed on L2 at 20:17:48 and DONE at 20:22:44 (03:22Z), with a test-only fix. CI stayed red for 44 more minutes: runs 39092490535, 39092732722, 39093406732, 39094005620 and 39094393407 were all cancelled by newer pushes before finishing, and green came with 39094990782 (created 03:49:55Z, success 04:06:17Z). Pushes every few minutes keep cancelling 10 to 15 minute CI runs, so a fixed branch keeps showing red.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table; Select-String -Path Z:\repos\Curl.audit\20261018-104525\logs\DarkFactory-*.log -Pattern '39091460067|BL-2707 (claim|DONE)'
```

- Expected: No red spell over 30 minutes.
- Actual: 2026-10-18T03:04:00Z 2026-10-18T04:06:17Z 62.28 39091460067 False; BL-2707 filed 20:11:45, DONE 20:22:44.

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
