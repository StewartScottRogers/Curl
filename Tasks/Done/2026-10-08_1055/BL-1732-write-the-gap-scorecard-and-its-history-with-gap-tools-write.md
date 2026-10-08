---
id: BL-1732
title: Write the gap scorecard and its history with Gap/Tools/Write-GapScorecard.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1720]
touches: [Gap/Tools/Write-GapScorecard.ps1, Gap/Tools/Fixtures/scorecard]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1732 — Write the gap scorecard and its history with Gap/Tools/Write-GapScorecard.ps1

## Goal

`Gap/Tools/Write-GapScorecard.ps1` writes one run's scorecard,
`Gap/Scorecards/<stamp>.md`, and appends the run to `Gap/Scorecards/history.json`. The
scorecard gives each area's "X of Y match", the overall percentage, the score against the
newest curl, and the change since the last run on the same platform.

## Context

This is ADR-0433 decisions 2 and 3. The scorecard's fixed sections, the `history.json`
entry, and how "against the newest version" is computed are all in
`Gap/Instructions/Gap-Format.md` and `Gap/Scorecards/README.md` (BL-1720).

**Parameters.** `-RunDirectory` (its `measurements/*.json`, and the optional
`release-<version>.json`), `-Stamp`, `-FindingsDirectory` (default `Gap/Findings`, read
after BL-1731 has run, for the New gaps, Closed and Regressions sections), `-ScorecardsDirectory`
(default `Gap/Scorecards`), `-NewestVersion` (default from `Gap/Baselines/newest.json`,
else the target), `-SelfTest`.

**Rules.**

- Per area: X = `counts.x` and Y = `counts.y` from its measurement, and % = 100 * X / Y,
  rounded to one decimal place. An area with Y = 0 prints `n/a` and adds nothing to the
  overall score.
- Overall = sum X over sum Y across the areas this run measured. An area the run did not
  measure is shown with its last measured score and the stamp of the run that measured it,
  marked `(carried from <stamp>)`, and is included in the overall score, so a partial run
  (`-Areas`) does not jump the trend.
- Change since last run: compare with the newest `history.json` entry for the same
  `platform`. Show `+n.n` or `-n.n` percentage points, or `first run` when there is none.
- Against the newest version: computed as `Gap-Format.md` says. When the newest version
  equals the target, this section says so and repeats the target figures.
- Unmeasured by reason: one table that sums each area's per-reason counts.
- Append exactly one entry to `history.json`, oldest first, creating the file as `[]` when
  missing. Never rewrite earlier entries.

**Self-test.** Fixtures under `Gap/Tools/Fixtures/scorecard/`: two measurement sets (a first
run and a later partial run), a findings folder and a history file, copied to a temporary
folder.

## Acceptance criteria

- [x] `Gap/Tools/Write-GapScorecard.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7. It checks: per-area X, Y and % for the fixture; overall is the sum of X over the sum of Y; an unmeasured area is carried with its stamp; the delta against the previous same-platform entry; `first run` when there is none; Y = 0 prints `n/a`; history gains exactly one entry and keeps the earlier ones byte-identical; the newest-version section follows `Gap-Format.md`.
- [x] The scorecard has exactly the fixed sections `Gap/Scorecards/README.md` lists, in that order.
- [x] The header help documents every parameter and rule. The script is ASCII only.

## Notes

Choices taken where the format left a default (all in the script's header help):

- A carried area prints `-` for unmeasured, excluded and change: history.json keeps only X and Y.
- Per-area change says `first run` when no same-platform entry measured that area; Y = 0 prints `n/a` for both % and change.
- Against the newest version uses one rule for every area: X = match items the diff neither removes nor changes; Y = non-excluded items the diff does not remove, plus the area's added keys. This yields section 10's inventory rule and its behaviour rule alike. Carried areas keep their target figures.
- Newest above target but no `release-<newest>.json` in the run: the section says so and repeats the target figures (nothing measured the difference).
- The release diff is read from `<run>/measurements/release-<v>.json` (Gap-Format section 5), falling back to `<run>/release-<v>.json`; `release-*.json` is never taken for an area measurement.
- Regressions: open findings with `regression: true` whose Log has a `- <stamp>:` line mentioning "reopen" (Gap-Format section 7 says the reopening run is logged).
- history.json is appended as text, so earlier bytes never change; a stamp already in history, or an existing scorecard, is refused so a rerun cannot append twice.
- Read-JsonFile assigns before returning so Windows PowerShell 5.1 unrolls a top-level JSON array.

Self-test passes under Windows PowerShell 5.1 and PowerShell 7 (21 checks).

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Write-GapScorecard.ps1 writes a run's scorecard and appends history.json; self-test passes on PS 5.1 and 7
