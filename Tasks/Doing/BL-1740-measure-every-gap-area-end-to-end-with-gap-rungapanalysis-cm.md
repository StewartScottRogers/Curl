---
id: BL-1740
title: Measure every gap area end to end with Gap/RunGapAnalysis.cmd
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1719, BL-1721, BL-1723, BL-1724, BL-1725, BL-1726, BL-1727, BL-1728, BL-1729, BL-1730]
touches: [Gap/RunGapAnalysis.ps1, Gap/RunGapAnalysis.cmd]
requirement: none
created: 2026-10-08
completed:
---
# BL-1740 — Measure every gap area end to end with Gap/RunGapAnalysis.cmd

## Goal

`Gap\RunGapAnalysis.cmd -NewTab` opens a herdr tab that prepares a detached tree of the
commit to measure, builds `Curl.Console` in Release, fetches the targeted curl release, and
runs every selected area's measurement tool. It leaves
`<repo>.gap\<stamp>\measurements\<area>.json` for each area. This is the measurement half of
the run; BL-1741 adds the analysts, findings, scorecard and tasks.

## Context

This is ADR-0433 decision 3. The model is `Audit\RunAudit.cmd` and `Audit/RunAudit.ps1`,
but a lane cannot open `Audit/`. What it establishes is restated here:

- The `.cmd` is one line:
  `powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RunGapAnalysis.ps1" %*`.
- **Refusals.** The script refuses inside a dark factory process (`CURL_DARK_FACTORY_LANE`
  set). It also refuses while a shift runs, detected as a process whose command line holds
  `RunDarkFactory.ps1` without `-NewTab` or `-Restart`, unless `-AlongsideShift` is given.
  `-SelfTest` checks both refusals on a faked process list.
- **`-NewTab`** starts the run in a new herdr tab when `HERDR_ENV=1`, and in a console
  window otherwise, then returns at once. Find how `RunDarkFactory.ps1` opens herdr tabs
  (search it for `HERDR_ENV`) and do the same. Never `Start-Process` a bare background job
  (CLAUDE.md, "Dark factory").
- **Logging.** Every step is traced to `<repo>.gap\<stamp>\gap.log`, where `<repo>.gap`
  sits beside the checkout as `<repo>.audit` does for audits, and `<stamp>` is
  `yyyy-MM-dd_HHmm`.

**Parameters.**

- `-Ref` (default `origin/work/dark-factory`): the commit measured.
- `-Areas` (any of `options`, `protocols`, `features`, `writeout`, `exitcodes`,
  `environment`, `behaviour`; default all). `protocols` and `features` come from one tool,
  so asking for either runs it.
- `-CurlVersion` (default `Gap/Baselines/target.json`'s version).
- `-AlongsideShift`, `-DryRun` (do the refusals, then print every step's command and change
  nothing), `-NewTab`, `-SelfTest`.

**Steps.**

1. Refusals. Then `git fetch origin`.
2. Create a detached worktree `<stamp>\tree` at `-Ref`. All measurement runs against that
   tree, never against the checkout the script was started from.
3. Build: `dotnet build <tree>/Curl.Console -c Release`.
4. `Gap/Tools/Get-UpstreamRelease.ps1 -Version <CurlVersion>`.
5. Find the reference curl with `Invoke-GapProbe.ps1`'s `Get-GapReferenceCurl`, and record
   its version line, or the docs fallback, in `<stamp>\run.json` with the commit, platform,
   target and areas.
6. Run each selected area's tool from the copy of `Gap/Tools/` beside this script, never
   from the measured tree, because the yardstick must not come from the commit being
   measured (BL-1741 makes that copy the `gap` branch's). Set `-Candidate` to the tree's
   Release binary and
   `-OutFile` (or `-OutDirectory`) set to `<stamp>\measurements\`:
   - `Measure-OptionGap.ps1`, `Measure-VersionGap.ps1`, `Measure-WriteOutGap.ps1`,
     `Measure-ExitCodeGap.ps1 -RepositoryRoot <tree>` and `Measure-EnvironmentGap.ps1`;
   - behaviour is three steps:
     `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <release>/tests/data <stamp>/raw.json`,
     then `ConvertTo-BehaviourMeasurement.ps1` (with the reference's feature and protocol
     names), then `Measure-ReferenceCrossCheck.ps1`.
   A tool that fails is logged, and its area is reported as not measured. The run goes on
   with the next area.
7. Print a summary: each area's X of Y and the run folder. Leave the tree worktree in place
   for BL-1741's steps. A `-Keep` switch is not needed: BL-1741 removes the worktree at the
   end.

## Acceptance criteria

- [ ] `Gap\RunGapAnalysis.cmd -DryRun` prints the refusal check, then every step's exact command for all seven areas, and changes nothing (`git status` and `git worktree list` are unchanged).
- [ ] `Gap\RunGapAnalysis.cmd -SelfTest` prints `PASS` lines and no `FAIL` for: refusal under `CURL_DARK_FACTORY_LANE`; refusal with a faked running shift; no refusal with `-AlongsideShift`; `-Areas protocols` runs the version tool once.
- [ ] A real run `Gap\RunGapAnalysis.cmd -Areas exitcodes,writeout -AlongsideShift` on Windows writes both measurements, `run.json` and `gap.log` under `<repo>.gap\<stamp>\`. Its summary is recorded in this task's Notes.
- [ ] The header help documents every parameter and step. The script is ASCII only and runs under Windows PowerShell 5.1.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
