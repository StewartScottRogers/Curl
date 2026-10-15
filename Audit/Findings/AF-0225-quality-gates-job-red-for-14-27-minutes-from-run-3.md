---
id: AF-0225
title: Quality gates job red for 14.27 minutes from run 38666857143 on aa14b025, and green again before its fix task ran
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:CI-run-38666857143:ci-red
reproduction: none
task: none
tasks:
found: 2026-10-15
found-at: 3e1062e68644127a82ba2f5ef33ceb1044cc6f67
scorecard: 2026-10-15_0156.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0225 - Quality gates job red for 14.27 minutes from run 38666857143 on aa14b025, and green again before its fix task ran

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: Quality gates job red for 14.27 minutes from run 38666857143 on aa14b025, and green again before its fix task ran.

## Evidence

Location: `logs/ci-runs.json`

The CI red rule is crossed (ciRedMinutes 170.52 over 60). Spell 2026-10-15T01:47:21Z to 02:01:37Z, 14.27 min, runId 38666857143: a failure on aa14b0255 (feat(console): resolve through CURL_DNS_SERVER ...) in job Quality gates (windows-latest), step Measure coverage, complexity and CRAP. The coordinator (DarkFactory-20261014-172412.log 18:49:58) saw it and filed BL-2251 at 18:54:08 local, 7 minutes after the failure. Run 38667881010 on 4c541e2e (chore(tasks): renumber task IDs) passed at 19:01:37 local, but BL-2251 was claimed only at 19:07:05 and finished at 19:09:38 (DarkFactory-20261014-172412-L2.log:93,102). So a commit that came before the fix task's commit ended the spell. The coordinator kept listing 38666857143 as a failure at 19:02:05 and 20:51:56.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-14 -LogRoot Z:\repos\Curl.audit\20261015-015611\logs -CiRunsJson Z:\repos\Curl.audit\20261015-015611\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table
```

- Expected: ciRedMinutes 60 or less.
- Actual: ciRedMinutes 170.52; spell 01:47:21Z to 02:01:37Z, 14.27 min, runId 38666857143.

## Re-audits

## Log

- 2026-10-15: filed proposed.
- 2026-10-15: proposed -> accepted.
