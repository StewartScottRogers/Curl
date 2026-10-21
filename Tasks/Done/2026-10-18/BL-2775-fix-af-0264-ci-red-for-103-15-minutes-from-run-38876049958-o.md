---
id: BL-2775
title: Fix AF-0264: CI red for 103.15 minutes from run 38876049958 on 241c183e (Quality gates); fix done in 16.7 min, then 5 cancelled runs
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
lane: no
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2775 — Fix AF-0264: CI red for 103.15 minutes from run 38876049958 on 241c183e (Quality gates); fix done in 16.7 min, then 5 cancelled runs

## Goal

The defect the audit office reported as AF-0264 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0264 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0264-ci-red-for-103-15-minutes-from-run-38876049958-on.md`.

Location: `logs/ci-runs.json`

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-16T09:02:13Z to 10:45:22Z, 103.15 min, run 38876049958, stillRed False. Runs in ci-runs.json: 38876049958 failure (241c183e), 38878261240 cancelled, 38880310277 failure (0c71f7fb), 38882357911, 38884026379, 38884492897 and 38886128544 cancelled, 38887776973 success at 10:45:22Z. This is the same incident as AF-0244 (Quality gates job, Libcurl CurlMulti.SetOption branch gap, BL-2466/BL-2467). The window starting 2026-10-16 includes it again; the coordinator log that recorded it is in the previous audit's copy (DarkFactory-20261015-200601.log). Reported because the rule crosses; it should close as a duplicate of AF-0244.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Format-Table
```

- Expected: No red spell over 30 minutes.
- Actual: 2026-10-16T09:02:13Z 2026-10-16T10:45:22Z 103.15 38876049958 False

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] AF-0264 is closed as a duplicate of AF-0244 (`status: closed`, `closed-how: duplicate`, `duplicate-of: AF-0244`) on the `audit` branch.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-19: Stewart had the first criterion reworded. It asked the reproduction to show no red spell, which a past spell can never do, so it now asks for AF-0264 to be closed as a duplicate of AF-0244.

- 2026-10-19: AF-0264 closed as a duplicate of AF-0244 (PR #173): the same CI red spell, run 38876049958, fixed by BL-2466 and BL-2467. The first criterion is not met and stays unticked: the reproduction measures 2026-10-16 onwards, which still holds that past spell, so it cannot give the expected result. Nothing is left to change. CI run 39188263654 (push) and 39188267132 (pull request) on audit commit ba08cc1d7 built the solution and passed the fast tests on Windows, Linux and macOS.

Interactive only: the reproduction runs a tool under the audit folder and the finding lives there, which a lane may not read (guard-audit-paths.ps1). The spell (2026-10-16, run 38876049958) is history: the Quality gates job was fixed by BL-2466/BL-2467 (same incident as AF-0244), so nothing in RunDarkFactory.ps1 is left to change. Remove `lane: no` once an interactive session has marked AF-0264 a duplicate of AF-0244 (or a re-audit closes it as historical).

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Backlog. Interactive only: reproduction and finding are in the audit folder, which lanes cannot read; incident is historical (AF-0244)
- 2026-10-18: Backlog -> Doing. Interactive session: AF-0264 is a duplicate of AF-0244. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Blocked. Stewart: AF-0264 closed as a duplicate of AF-0244, so nothing is left to change, but the criterion that the reproduction show no red spell cannot be met: it measures from 2026-10-16, which holds that past spell. Drop this task (Deferred), or reword the criterion?
- 2026-10-18: Blocked -> Backlog. Stewart: reword the reproduction criterion, which a past spell can never meet.
- 2026-10-18: Backlog -> Doing. Interactive session, with the reworded criterion. Criteria at claim: 701b3869 9742157d.
- 2026-10-18: Doing -> Done. AF-0264 closed as a duplicate of AF-0244 on the audit branch (PR #173); nothing is left to change.
