---
id: AF-0165
title: BL-2035's run was cut off 33 s after its claim when the lane script crashed ('Cannot bind argument to parameter Path'), leaving the task idle until the next shift
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-2035:BL-2035:unfinished-run
reproduction: none
task: none
tasks:
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0165 - BL-2035's run was cut off 33 s after its claim when the lane script crashed ('Cannot bind argument to parameter Path'), leaving the task idle until the next shift

## Summary

Low finding from the process auditor at `logs/BL-2035-20261010-161355-L1.jsonl`: BL-2035's run was cut off 33 s after its claim when the lane script crashed ('Cannot bind argument to parameter Path'), leaving the task idle until the next shift.

## Evidence

Location: `logs/BL-2035-20261010-161355-L1.jsonl`

unfinishedRuns includes BL-2035-20261010-161355-L1.jsonl. The run's first event is at 23:26:35Z and its last at 23:27:01Z, mid-Edit on UpstreamCaseRunner.cs. It has 0 'type':'result' events. The lane trace shows the claim at 16:26:28, then at 16:27:01 'ERROR Cannot bind argument to parameter 'Path' because it is an empty strin~' and 'shift end (script error) ... stalled=1' (DarkFactory-20261010-161355-L1.log:32-33). The claim came 12 s after 'renum BL-2033 -> BL-2035' on the same lane. The coordinator logged 'lane 1 ended (script error)' at 16:45:31. The next shift took over BL-2035 at 18:17:16 ('lane 1 adopts BL-2035 from the previous shift, work in place') and integrated it at 18:19:44 (commit 39647f9ac), so the task was recovered. Still, about 1 h 50 min passed with the task in Doing and no lane on it. Cause: a crash in the lane script itself, not a timeout or a usage limit.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-10 -LogRoot Z:\repos\Curl.audit\20261011-005116\logs -CiRunsJson Z:\repos\Curl.audit\20261011-005116\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Select-String -Path Z:\repos\Curl.audit\20261011-005116\logs\DarkFactory-20261010-161355-L1.log -Pattern 'ERROR|script error'
```

- Expected: unfinishedRuns is empty, and no lane ends with a script error.
- Actual: unfinishedRuns: BL-2035-20261010-161355-L1.jsonl, BL-2999-20261010-181711-L9.jsonl; '16:27:01 L1 - ERROR Cannot bind argument to parameter 'Path' because it is an empty strin~', '16:27:01 L1 - shift end (script error) done=1 blocked=0 requeued=1 stalled=1'

## Re-audits

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
