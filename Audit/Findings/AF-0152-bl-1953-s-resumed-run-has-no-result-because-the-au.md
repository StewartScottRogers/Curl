---
id: AF-0152
title: BL-1953's resumed run has no result because the audit copied the logs 1 s after it started
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-11_0051.md: the reproduction no longer reproduces.
key: process:BL-1953:BL-1953-resumed-run:unfinished-run
reproduction: none
task: none
tasks:
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed: 2026-10-11
closed-how: reliable-reaudit
closed-by: 2026-10-11_0051.md
---
# AF-0152 - BL-1953's resumed run has no result because the audit copied the logs 1 s after it started

## Summary

Low finding from the process auditor at `logs/BL-1953-20261010-012305-L1-resumed.jsonl`: BL-1953's resumed run has no result because the audit copied the logs 1 s after it started.

## Evidence

Location: `logs/BL-1953-20261010-012305-L1-resumed.jsonl`

unfinishedRuns lists BL-1953-20261010-012305-L1-resumed.jsonl: 9 lines, only SessionStart hook and init events, .err.txt empty. DarkFactory-20261010-012305-L1.log: 01:23:25 'BL-1953 resume this lane held the task when it stopped'. The audit run stamp is 20261010-012326, so the log copy was taken about 1 s after the resume. This is a run still in progress at copy time, not a crash or timeout. It follows the restart of shift 20261010-011807, whose coordinator log ends at 01:18:48 without a stop line.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot Z:\repos\Curl.audit\20261010-012326\logs -CiRunsJson Z:\repos\Curl.audit\20261010-012326\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns
```

- Expected: unfinishedRuns empty
- Actual: BL-1953-20261010-011807-L1.jsonl, BL-1953-20261010-012305-L1-resumed.jsonl

## Re-audits

- 2026-10-11 | 2026-10-11_0051.md | reproduces: no | The reproduction on the 20261010-012326 logs lists BL-1953-20261010-011807-L1.jsonl and BL-1953-20261010-012305-L1-resumed.jsonl. In this window the resumed run has a result and is not in unfinishedRuns. Ran the rule over this window: no unfinished run was cut short by the log copy. The last shift (DarkFactory-20261010-213213) ended at 22:01:28, about 3 h before the copy (log folder written 00:53 on 10-11). BL-2035's run stopped from a lane script error, and BL-2999's file matches no shift or claim (see its finding).

## Log

- 2026-10-10: filed proposed.
- 2026-10-11: proposed -> closed. Re-audit 2026-10-11_0051.md: the reproduction no longer reproduces.
