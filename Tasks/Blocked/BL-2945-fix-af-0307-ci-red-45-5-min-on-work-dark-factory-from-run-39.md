---
id: BL-2945
title: Fix AF-0307: CI red 45.5 min on work/dark-factory from run 39226925458 (SslsImportThenExport_OnTheManagedEngineOffWindows...)
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-21
completed:
---
# BL-2945 — Fix AF-0307: CI red 45.5 min on work/dark-factory from run 39226925458 (SslsImportThenExport_OnTheManagedEngineOffWindows...)

## Goal

The defect the audit office reported as AF-0307 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0307 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0307-ci-red-45-5-min-on-work-dark-factory-from-run-3922.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells[1]: start 2026-10-19T09:24:46Z, end 10:10:14Z, 45.47 min, runId 39226925458, head de41660b ('chore(tasks): finish BL-2784'). Failing test: SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBack..., which runs only off Windows, so lanes could not see it. Timeline (MST): red at 02:24:46. BL-2794 filed at 02:27:28 (DarkFactory-20261018-192454.log:104). Claimed by L1 only at 02:45:01, 17.5 min after filing. Done and pushed at 02:49:17. Runs on 5daf5ac2 (39229063830) and e5ca379c (39232372348) also failed. Green with run 39232916919 on 2ee6415c at 03:10:14.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 39226925458
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-19T09:24:46Z end 2026-10-19T10:10:14Z minutes 45.47 runId 39226925458 stillRed False

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Investigated 2026-10-21 (shift log DarkFactory-20261018-192454.log lines 104-125). Detection was fast: the CI watch filed BL-2794 2.7 min after the run finished. The 45.5 min red spell was: one lane busy with a task that overlapped BL-2794 touches (board capacity stayed 1, so no second lane could start it; the board already holds other work back behind a waiting task), then 17.5 min wait, then about 21 min of CI on the fix commit, with the two commits pushed meanwhile (runs 39229063830, 39232372348) also failing because they predate the fix. No RunDarkFactory.ps1 change shortens that without pre-empting a running task or testing off Windows in lanes; the 30 min gate is the audit office threshold (Audit paths, not factory-editable). The reproduction reads logs of the past run, so no code change can alter it.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Blocked. Stewart: accept AF-0307 as inherent (CI about 21 min plus a busy overlapping lane), or authorise pre-empting a running lane for High CI-fix tasks?
