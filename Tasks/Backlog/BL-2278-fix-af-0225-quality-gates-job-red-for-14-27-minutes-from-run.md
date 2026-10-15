---
id: BL-2278
title: Fix AF-0225: Quality gates job red for 14.27 minutes from run 38666857143 on aa14b025, and green again before its fix task ran
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-15
completed:
---
# BL-2278 — Fix AF-0225: Quality gates job red for 14.27 minutes from run 38666857143 on aa14b025, and green again before its fix task ran

## Goal

The defect the audit office reported as AF-0225 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0225 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0225-quality-gates-job-red-for-14-27-minutes-from-run-3.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

The CI red rule is crossed (ciRedMinutes 170.52 over 60). Spell 2026-10-15T01:47:21Z to 02:01:37Z, 14.27 min, runId 38666857143: a failure on aa14b0255 (feat(console): resolve through CURL_DNS_SERVER ...) in job Quality gates (windows-latest), step Measure coverage, complexity and CRAP. The coordinator (DarkFactory-20261014-172412.log 18:49:58) saw it and filed BL-2251 at 18:54:08 local, 7 minutes after the failure. Run 38667881010 on 4c541e2e (chore(tasks): renumber task IDs) passed at 19:01:37 local, but BL-2251 was claimed only at 19:07:05 and finished at 19:09:38 (DarkFactory-20261014-172412-L2.log:93,102). So a commit that came before the fix task's commit ended the spell. The coordinator kept listing 38666857143 as a failure at 19:02:05 and 20:51:56.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-14 -LogRoot Z:\repos\Curl.audit\20261015-015611\logs -CiRunsJson Z:\repos\Curl.audit\20261015-015611\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table
```

- Expected: ciRedMinutes 60 or less.
- Actual: ciRedMinutes 170.52; spell 01:47:21Z to 02:01:37Z, 14.27 min, runId 38666857143.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Interactive only (lane: no). Read from the code and the task text, the 14.27-minute spell looks like a one-off: run 38666857143 failed on aa14b025 (a lane pushed past the Quality gates), the coordinator filed BL-2251 after its 5-minute confirm wait (`$CiConfirmMinutes`), and an unrelated later commit passed first, ending the spell. `RunDarkFactory.ps1` already files a High "Fix CI failure" task, which `next` offers first and which runs on opus; AF-0213 already requires a quality run before a push. I found no defect in `RunDarkFactory.ps1` to change. The reproduction runs the process auditor's measuring tool over the audit worktree's logs, which the audit guard stops a lane from reading, so a lane cannot check the acceptance criterion. Remove `lane: no` once an interactive session has run the reproduction and either decided the spell needs no factory change (then close the task) or named the change to make.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Backlog. Interactive only: the reproduction reads audit paths a lane cannot read; no defect found in RunDarkFactory.ps1
