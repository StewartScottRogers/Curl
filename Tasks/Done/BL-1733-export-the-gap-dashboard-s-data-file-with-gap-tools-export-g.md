---
id: BL-1733
title: Export the gap dashboard's data file with Gap/Tools/Export-GapDashboardData.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1720]
touches: [Gap/Tools/Export-GapDashboardData.ps1, Gap/Tools/Fixtures/dashboard]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1733 — Export the gap dashboard's data file with Gap/Tools/Export-GapDashboardData.ps1

## Goal

`Gap/Tools/Export-GapDashboardData.ps1 -OutFile <path>/data.json` writes the one JSON file
the gap dashboard page (BL-1742) reads. It is built from `Gap/Scorecards/history.json`,
`Gap/Findings/` and `Gap/Baselines/`.

## Context

This is ADR-0433 decision 8. The `data.json` shape is in `Gap/Instructions/Gap-Format.md`
(BL-1720), section "Dashboard data". The page is published beside this file on the GitHub
Pages site (`gaps/data.json`, BL-1743). The weekly release watcher (BL-1736) also runs this
script, on `ubuntu-latest` under `pwsh`, so it must work on Linux.

**Parameters.** `-FindingsDirectory` (default `Gap/Findings`), `-ScorecardsDirectory`
(default `Gap/Scorecards`), `-BaselinesDirectory` (default `Gap/Baselines`),
`-RepositoryUrl` (default `https://github.com/StewartScottRogers/Curl`), `-Branch` (default
`gap`, used to build each finding's `url` as `<repo>/blob/<branch>/Gap/Findings/<file>`),
`-OutFile`, `-SelfTest`.

**Rules.**

- `open`: every `status: open` finding, sorted by severity (Critical first), then
  `scope` (`target` before `newest`), then ID. Each one carries its suggestion text in full
  (the dashboard shows it) and its task IDs.
- `closed`: findings closed in the last five runs of `history.json`.
- `regressions`: open findings with `regression: true`.
- `release`: `{ version, newGaps }` when `newest.json` names a version above the target,
  where `newGaps` counts the open `scope: newest` findings with that `introduced-in`.
  Otherwise `null`.
- With no history yet, write `latest: null` and `history: []`, and the other lists from
  whatever findings exist. The page shows "No gap analysis has run yet".
- The output is deterministic: the same inputs give byte-identical output apart from
  `generated`.

**Self-test.** Fixtures under `Gap/Tools/Fixtures/dashboard/`: a findings folder with open,
closed, rejected, regression and newest-scope findings, a history file and baselines. The
fixture's output also serves as the page's preview data, so BL-1742 copies it to
`.github/gaps/fixtures/data.json`.

## Acceptance criteria

- [x] `Gap/Tools/Export-GapDashboardData.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7. It checks: the open list's order; rejected findings are absent; `closed` holds only the last five runs; regressions are listed; `release` is set only when newest is above the target; the no-history case; two runs give identical output apart from `generated`.
- [x] The output validates against `Gap-Format.md`'s dashboard data section, and `Gap/Tools/Fixtures/dashboard/expected-data.json` is committed as the fixture's output.
- [x] The header help documents every parameter. The script is ASCII only and uses no Windows-only API.

## Notes

- The script writes JSON with its own small writer, not `ConvertTo-Json`: Windows
  PowerShell 5.1 and PowerShell 7 format and escape differently, and the output must be
  byte-identical on both (UTF-8 without BOM, LF, two-space indents, non-ASCII as `\uXXXX`).
- Sorting uses an ordinal string key (severity rank, scope rank, ID), so the order never
  depends on the culture. `[System.Array]::Sort(keys, items)` silently left the items
  unsorted when called from PowerShell, so a list of keys is sorted instead.
- `closed` takes findings whose `closed` stamp is one of the last five `history.json`
  stamps. When `target.json` or `newest.json` is missing, its version falls back to the
  last history entry, else `null`.
- `open[].suggestion` is the Suggestion section's text with lines joined by `\n` and
  outer blank lines trimmed.
- `expected-data.json` is the fixture's output with `generated` fixed at
  `2026-10-09T15:00:00Z`; the self-test generates with that time and compares byte for byte
  (line endings normalised, so a CRLF checkout still passes). BL-1742 copies it as the
  page's preview data.
- Verified: `-SelfTest` prints 15 PASS lines and exits 0 under `powershell` 5.1 and `pwsh` 7.
  `dotnet build` clean; fast tests green. No `.cs` or project file changed; `Gap.projitems`
  globs every file, so the new files need no project edit.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Export-GapDashboardData.ps1 writes the dashboard's data.json deterministically; its 15-check self-test passes under PowerShell 5.1 and 7
