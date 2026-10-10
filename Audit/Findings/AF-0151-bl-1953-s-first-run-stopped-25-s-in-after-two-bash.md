---
id: AF-0151
title: BL-1953's first run stopped 25 s in, after two Bash calls returned exit code 107, with no result; the lane called it an API failure
auditor: process
severity: Low
status: accepted
reason: 
key: process:BL-1953:BL-1953-first-run:unfinished-run
reproduction: none
task: BL-1970
tasks: BL-1970
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0151 - BL-1953's first run stopped 25 s in, after two Bash calls returned exit code 107, with no result; the lane called it an API failure

## Summary

Low finding from the process auditor at `logs/BL-1953-20261010-011807-L1.jsonl`: BL-1953's first run stopped 25 s in, after two Bash calls returned exit code 107, with no result; the lane called it an API failure.

## Evidence

Location: `logs/BL-1953-20261010-011807-L1.jsonl`

unfinishedRuns lists BL-1953-20261010-011807-L1.jsonl: 14 lines, no result event, .err.txt empty. Claimed 01:18:41 PDT (DarkFactory-20261010-011807-L1.log). Two Bash tool calls ('ls Tasks/Doing/ && cat Tasks/Doing/BL-1953* && grep -l "BL-1916" -r ...') both returned 'Exit code 107', and the stream stopped at 08:19:06Z. At 01:19:06 the lane logged 'run failed on the API; waiting until Claude answers again'. But the run's own rate_limit_event says status 'allowed' with five_hour utilization 0, so the API-failure label is probably wrong. The next shift (01:23:23) adopted the lane, so the stop was recovered.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot Z:\repos\Curl.audit\20261010-012326\logs -CiRunsJson Z:\repos\Curl.audit\20261010-012326\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261010-012326\logs\BL-1953-20261010-011807-L1.jsonl -Pattern 'Exit code 107|"type":"result"'
```

- Expected: Every run log ends with a result event; unfinishedRuns empty
- Actual: unfinishedRuns contains BL-1953-20261010-011807-L1.jsonl; two 'Exit code 107' tool results and no result event

## Re-audits

## Log

- 2026-10-10: filed proposed.
- 2026-10-10: proposed -> accepted.
