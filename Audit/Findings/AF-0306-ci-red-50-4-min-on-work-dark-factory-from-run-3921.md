---
id: AF-0306
title: CI red 50.4 min on work/dark-factory from run 39210403935 (Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwice...)
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:39210403935:ci-red
reproduction: none
task: none
tasks:
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0306 - CI red 50.4 min on work/dark-factory from run 39210403935 (Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwice...)

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 50.4 min on work/dark-factory from run 39210403935 (Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwice...).

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells[0]: start 2026-10-19T06:50:57Z, end 07:41:20Z, 50.38 min, runId 39210403935, head 1c3d8cc6 ('chore(tasks): requeue BL-2744 ...'). The commit only carried the regression; it did not cause it. Failing test: Perform_AfterAPerformWhoseDigestAnswerWasRefused_SendsTheKeptNonceTwice..., off Windows. Timeline (MST): red at 23:50:57. BL-2790 filed at 23:53:15 (DarkFactory-20261018-192454.log:71). Claimed by L1 at 23:58:18, Done and pushed at 00:04:20, 13.4 min after red. Run 39212464400 on 91b509f6 also failed. Runs 39213638245 and 39214116664 were cancelled by newer pushes. Green came only with run 39214162783 on 7d6338c7 at 00:41:20. Most of the spell (37 min) was CI on the fix commit, not repair time.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-18 -LogRoot Z:\repos\Curl.audit\20261019-213437\logs -CiRunsJson Z:\repos\Curl.audit\20261019-213437\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 39210403935
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-19T06:50:57Z end 2026-10-19T07:41:20Z minutes 50.38 runId 39210403935 stillRed False

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: yes | The original reproduction gives spell 39210403935: 50.38 min. This window has the same mechanism, a lane push turning CI red for over 30 minutes before a fix landed. Spell 39691487971 lasted 48.35 min (2026-10-21T23:21:56Z to 2026-10-22T00:10:17Z) after commit 01deb2980; the Windows quality gate was fixed by BL-2998. Spell 39718635967 lasted 46.02 min after d6d35bf61.

## Log

- 2026-10-19: filed proposed.
