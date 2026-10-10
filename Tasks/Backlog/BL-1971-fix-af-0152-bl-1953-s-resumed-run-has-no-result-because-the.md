---
id: BL-1971
title: Fix AF-0152: BL-1953's resumed run has no result because the audit copied the logs 1 s after it started
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-10
completed:
---
# BL-1971 — Fix AF-0152: BL-1953's resumed run has no result because the audit copied the logs 1 s after it started

## Goal

The defect the audit office reported as AF-0152 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0152 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0152-bl-1953-s-resumed-run-has-no-result-because-the-au.md`.

Location: `logs/BL-1953-20261010-012305-L1-resumed.jsonl`

Location: `logs/BL-1953-20261010-012305-L1-resumed.jsonl`

unfinishedRuns lists BL-1953-20261010-012305-L1-resumed.jsonl: 9 lines, only SessionStart hook and init events, .err.txt empty. DarkFactory-20261010-012305-L1.log: 01:23:25 'BL-1953 resume this lane held the task when it stopped'. The audit run stamp is 20261010-012326, so the log copy was taken about 1 s after the resume. This is a run still in progress at copy time, not a crash or timeout. It follows the restart of shift 20261010-011807, whose coordinator log ends at 01:18:48 without a stop line.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot Z:\repos\Curl.audit\20261010-012326\logs -CiRunsJson Z:\repos\Curl.audit\20261010-012326\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns
```

- Expected: unfinishedRuns empty
- Actual: BL-1953-20261010-011807-L1.jsonl, BL-1953-20261010-012305-L1-resumed.jsonl

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-10: Created.
