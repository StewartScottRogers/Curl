---
id: BL-2058
title: Fix AF-0165: BL-2035's run was cut off 33 s after its claim when the lane script crashed ('Cannot bind argument to parameter Path'), leaving the task idle until the next shift
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-11
completed:
---
# BL-2058 — Fix AF-0165: BL-2035's run was cut off 33 s after its claim when the lane script crashed ('Cannot bind argument to parameter Path'), leaving the task idle until the next shift

## Goal

The defect the audit office reported as AF-0165 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0165 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0165-bl-2035-s-run-was-cut-off-33-s-after-its-claim-whe.md`.

Location: `logs/BL-2035-20261010-161355-L1.jsonl`

Location: `logs/BL-2035-20261010-161355-L1.jsonl`

unfinishedRuns includes BL-2035-20261010-161355-L1.jsonl. The run's first event is at 23:26:35Z and its last at 23:27:01Z, mid-Edit on UpstreamCaseRunner.cs. It has 0 'type':'result' events. The lane trace shows the claim at 16:26:28, then at 16:27:01 'ERROR Cannot bind argument to parameter 'Path' because it is an empty strin~' and 'shift end (script error) ... stalled=1' (DarkFactory-20261010-161355-L1.log:32-33). The claim came 12 s after 'renum BL-2033 -> BL-2035' on the same lane. The coordinator logged 'lane 1 ended (script error)' at 16:45:31. The next shift took over BL-2035 at 18:17:16 ('lane 1 adopts BL-2035 from the previous shift, work in place') and integrated it at 18:19:44 (commit 39647f9ac), so the task was recovered. Still, about 1 h 50 min passed with the task in Doing and no lane on it. Cause: a crash in the lane script itself, not a timeout or a usage limit.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-10 -LogRoot Z:\repos\Curl.audit\20261011-005116\logs -CiRunsJson Z:\repos\Curl.audit\20261011-005116\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Select-String -Path Z:\repos\Curl.audit\20261011-005116\logs\DarkFactory-20261010-161355-L1.log -Pattern 'ERROR|script error'
```

- Expected: unfinishedRuns is empty, and no lane ends with a script error.
- Actual: unfinishedRuns: BL-2035-20261010-161355-L1.jsonl, BL-2999-20261010-181711-L9.jsonl; '16:27:01 L1 - ERROR Cannot bind argument to parameter 'Path' because it is an empty strin~', '16:27:01 L1 - shift end (script error) done=1 blocked=0 requeued=1 stalled=1'

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-11: Created.
