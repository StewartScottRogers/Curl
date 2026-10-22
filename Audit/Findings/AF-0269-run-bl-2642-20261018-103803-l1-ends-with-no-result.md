---
id: AF-0269
title: Run BL-2642-20261018-103803-L1 ends with no result: it was still running when the logs were copied
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-22_0130.md: the reproduction no longer reproduces.
key: process:logs:BL-2642:unfinished-run
reproduction: none
task: BL-2780
tasks: BL-2780
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-22
closed-how: reliable-reaudit
closed-by: 2026-10-22_0130.md
---
# AF-0269 - Run BL-2642-20261018-103803-L1 ends with no result: it was still running when the logs were copied

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261018-103803-L1.log:2`: Run BL-2642-20261018-103803-L1 ends with no result: it was still running when the logs were copied.

## Evidence

Location: `logs/DarkFactory-20261018-103803-L1.log:2`

unfinishedRuns: BL-2642-20261018-103803-L1.jsonl. The lane claimed BL-2642 at 10:38:23, applied the earlier run's stash 0ae87ed0 at 10:38:24, and its last lane log line is 'quality ok' at 10:45:19. The run log's last record, at 2026-10-18T17:45:30Z (10:45:30 local), is a Bash tool call in progress, with no result record. The log copy was taken at about 10:45. So the run was cut off by the audit's snapshot, not by a timeout, usage limit or crash. The shift (DarkFactory-20261018-103803.log) was still running. Earlier, BL-2642's first run (07:06:41 to 07:16:21, DarkFactory-20261018-012310-L1.log:547-563) requeued to wait on BL-2727, and this run resumes it from stash. Separately, the 10:31:37 handover shift refused to start ('working tree not clean', ALARM stage 1; DarkFactory-20261018-103136.log), costing 6.5 minutes before the 10:38 shift.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).unfinishedRuns; Get-Content Z:\repos\Curl.audit\20261018-104525\logs\DarkFactory-20261018-103803-L1.log
```

- Expected: unfinishedRuns empty.
- Actual: BL-2642-20261018-103803-L1.jsonl; lane log ends at 10:45:19 'quality ok' with no end, DONE or REQUEUE.

## Re-audits

- 2026-10-18 | 2026-10-18_1905.md | not re-audited | overlaps planted defect PD-503 in BL-9990-20261018-181500-L9.jsonl, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction: the tool now gives unfinishedRuns empty even for the 2026-10-16 window, and BL-2642-20261018-103803-L1.jsonl in this copy ends with a result line and 'FACTORY: DONE BL-2642 ...'. Over this window runningRuns is empty, so no run of a live shift was cut off by the log copy. The one entry in unfinishedRuns (BL-9990-20261018-181500-L9.jsonl) has a different cause: no shift or lane owns it, and it was written 15 minutes after the only shift ended. It is filed as a new finding.
- 2026-10-19 | 2026-10-19_2134.md | not re-audited | overlaps planted defect PD-503 in BL-2714-20261019-160000-L9.jsonl, so the auditor's verdict (reproduces no) is set aside: I ran the given reproduction on Z:\repos\Curl.audit\20261018-104525\logs. unfinishedRuns is now empty and BL-2642-20261018-103803-L1.jsonl is listed under runningRuns. Its shift log DarkFactory-20261018-103803.log has 0 'shift end' lines, and the L1 log ends at 10:45:19 'quality ok', so it is a run still in progress, not counted as unfinished. Mechanism over this window (-Since 2026-10-18; 93 tasks done in shifts 192454, 061607, 092012, 094022, 095619, 112913, 150735, 192417, 211325 and 212549): unfinishedRuns holds only BL-2714-20261019-160000-L9.jsonl. That is not a run still in progress at copy: it has no shift and its task was already Done. So the cause, a run still in progress reported as unfinished, did not recur. The reverse misreading did happen this window: stopped shift 191652's BL-2850 run is listed as running (finding process:logs:BL-2850:unfinished-run).
- 2026-10-22 | 2026-10-22_0130.md | reproduces: no | Ran the given command on the 20261018-104525 logs: unfinishedRuns is now empty there, and DarkFactory-20261018-103803-L1.log still ends at 10:45:19 'BL-2642 quality ok'. Checked the mechanism over this window (since 2026-10-19), which has 150 tasks done: unfinishedRuns = [] and runningRuns = [], and every BL-*.jsonl in the log copy has a result line. No run ends with no result.

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-22: accepted -> closed. Re-audit 2026-10-22_0130.md: the reproduction no longer reproduces.
