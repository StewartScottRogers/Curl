---
id: AF-0307
title: CI red 45.5 min on work/dark-factory from run 39226925458 (SslsImportThenExport_OnTheManagedEngineOffWindows...)
auditor: process
severity: Low
status: closed
reason: Stewart closed it on 2026-10-21: he accepts the 45.5 min red spell as inherent (about 21 min of CI on the fix plus a busy overlapping lane) rather than have High CI-fix tasks pre-empt a running lane; BL-2945 recorded the decision.
key: process:logs:39226925458:ci-red
reproduction: none
task: BL-2945
tasks: BL-2945
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed: 2026-10-21
closed-how: stewart
closed-by: stewart
---
# AF-0307 - CI red 45.5 min on work/dark-factory from run 39226925458 (SslsImportThenExport_OnTheManagedEngineOffWindows...)

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 45.5 min on work/dark-factory from run 39226925458 (SslsImportThenExport_OnTheManagedEngineOffWindows...).

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells[1]: start 2026-10-19T09:24:46Z, end 10:10:14Z, 45.47 min, runId 39226925458, head de41660b ('chore(tasks): finish BL-2784'). Failing test: SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBack..., which runs only off Windows, so lanes could not see it. Timeline (MST): red at 02:24:46. BL-2794 filed at 02:27:28 (DarkFactory-20261018-192454.log:104). Claimed by L1 only at 02:45:01, 17.5 min after filing. Done and pushed at 02:49:17. Runs on 5daf5ac2 (39229063830) and e5ca379c (39232372348) also failed. Green with run 39232916919 on 2ee6415c at 03:10:14.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 39226925458
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-19T09:24:46Z end 2026-10-19T10:10:14Z minutes 45.47 runId 39226925458 stillRed False

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: yes | still reported
- 2026-10-22 | 2026-10-22_0130.md | reproduces: yes | The original reproduction gives spell 39226925458: 45.47 min, and this window (since 2026-10-19) contains that spell again. The same cause, a test failing only off Windows where lanes never test, crosses the rule again with spell 39362208615: test307 and test1587 on Linux and macOS, 2034.67 min, ended 2026-10-21T13:23:52Z.

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
- 2026-10-21: accepted -> closed. Stewart accepts the red spell as inherent; no lane pre-emption.
