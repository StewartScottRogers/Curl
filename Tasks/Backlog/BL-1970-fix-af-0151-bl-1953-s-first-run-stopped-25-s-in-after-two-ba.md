---
id: BL-1970
title: Fix AF-0151: BL-1953's first run stopped 25 s in, after two Bash calls returned exit code 107, with no result; the lane called it an API failure
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-10
completed:
---
# BL-1970 — Fix AF-0151: BL-1953's first run stopped 25 s in, after two Bash calls returned exit code 107, with no result; the lane called it an API failure

## Goal

The defect the audit office reported as AF-0151 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0151 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0151-bl-1953-s-first-run-stopped-25-s-in-after-two-bash.md`.

Location: `logs/BL-1953-20261010-011807-L1.jsonl`

Location: `logs/BL-1953-20261010-011807-L1.jsonl`

unfinishedRuns lists BL-1953-20261010-011807-L1.jsonl: 14 lines, no result event, .err.txt empty. Claimed 01:18:41 PDT (DarkFactory-20261010-011807-L1.log). Two Bash tool calls ('ls Tasks/Doing/ && cat Tasks/Doing/BL-1953* && grep -l "BL-1916" -r ...') both returned 'Exit code 107', and the stream stopped at 08:19:06Z. At 01:19:06 the lane logged 'run failed on the API; waiting until Claude answers again'. But the run's own rate_limit_event says status 'allowed' with five_hour utilization 0, so the API-failure label is probably wrong. The next shift (01:23:23) adopted the lane, so the stop was recovered.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot Z:\repos\Curl.audit\20261010-012326\logs -CiRunsJson Z:\repos\Curl.audit\20261010-012326\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261010-012326\logs\BL-1953-20261010-011807-L1.jsonl -Pattern 'Exit code 107|"type":"result"'
```

- Expected: Every run log ends with a result event; unfinishedRuns empty
- Actual: unfinishedRuns contains BL-1953-20261010-011807-L1.jsonl; two 'Exit code 107' tool results and no result event

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-10: Created.
