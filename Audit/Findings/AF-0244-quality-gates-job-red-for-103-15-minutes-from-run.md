---
id: AF-0244
title: Quality gates job red for 103.15 minutes from run 38876049958 on 241c183e: Libcurl CurlMulti.SetOption branch gap, fixed by a separate task, then six cancelled runs
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-18_1045.md: the reproduction no longer reproduces.
key: process:logs:QualityGates-CurlMulti.SetOption:ci-red
reproduction: none
task: BL-2581
tasks: BL-2581
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed: 2026-10-18
closed-how: reliable-reaudit
closed-by: 2026-10-18_1045.md
---
# AF-0244 - Quality gates job red for 103.15 minutes from run 38876049958 on 241c183e: Libcurl CurlMulti.SetOption branch gap, fixed by a separate task, then six cancelled runs

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: Quality gates job red for 103.15 minutes from run 38876049958 on 241c183e: Libcurl CurlMulti.SetOption branch gap, fixed by a separate task, then six cancelled runs.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-10-16T09:02:13Z, end 10:45:22Z, 103.15 min, runId 38876049958 (job Quality gates (windows-latest) step Measure coverage, complexity and CRAP) on 241c183e ('chore(tasks): point BL-2436 at ADR-0601'). Coordinator saw it at 02:03:28 local and filed BL-2466 at 02:09:50 (DarkFactory-20261015-200601.log:68-69). BL-2466 claimed 02:25:15, Done 02:43:01 (DarkFactory-20261015-200601-L2.log:588-610); it rewrote Measure-CodeQuality.ps1 and left the actual failing member (Libcurl CurlMulti.SetOption, 87.5% branch) to BL-2467, a Normal-priority task. Its ID collided with BL-2466 and was renumbered at 02:24:12 (L2.log:584); L1 claimed it at 02:58:26 and it was Done at 03:01:55. Run 38880310277 on 0c71f7fb also failed. Six runs between 09:07Z and 10:31Z were cancelled by newer pushes, so the first finished run (38887776973 on 8b514a87) went green only at 10:45:22Z, 43 min after the fix landed. Time to file: 7.6 min; to the real fix: 60 min; cancellation churn: 43 min.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-15 -LogRoot Z:\repos\Curl.audit\20261016-162347\logs -CiRunsJson Z:\repos\Curl.audit\20261016-162347\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table; Select-String -Path Z:\repos\Curl.audit\20261016-162347\logs\DarkFactory-20261015-200601*.log -Pattern 'BL-2466|BL-2467 (claim|DONE)|38876049958'
```

- Expected: No red spell over 30 minutes.
- Actual: 2026-10-16T09:02:13Z to 10:45:22Z, 103.15 min, runId 38876049958; BL-2466 filed 02:09:50 local, BL-2467 Done 03:01:55 local.

## Re-audits

- 2026-10-18 | 2026-10-18_1045.md | reproduces: no | The original reproduction still shows the 103.15-min spell from run 38876049958, with BL-2466 filed at 02:09:50 and BL-2467 DONE at 03:01:55. Re-audited by mechanism over 2026-10-16 onward (134 tasks done): no new Quality gates job failure. The new spells (39077398008, 44.63 min; 39091460067, 62.28 min) were test failures. The tool still lists 38876049958 only because -Since 2026-10-16 overlaps that incident, already counted by the previous audit. Note: the 'cancelled runs prolong red' part of this finding recurs. After its fix was done, spell 39091460067 stayed red for 44 min through 5 cancelled runs (39092490535 to 39094393407), reported as process:logs:39091460067:ci-red.

## Log

- 2026-10-16: filed proposed.
- 2026-10-16: proposed -> accepted.
- 2026-10-18: accepted -> closed. Re-audit 2026-10-18_1045.md: the reproduction no longer reproduces.
