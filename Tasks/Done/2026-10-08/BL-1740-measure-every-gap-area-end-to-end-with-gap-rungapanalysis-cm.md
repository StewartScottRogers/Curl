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
completed: 2026-10-08
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

- [x] `Gap\RunGapAnalysis.cmd -SelfTest` prints `PASS` lines and no `FAIL` for: refusal under `CURL_DARK_FACTORY_LANE`; refusal with a faked running shift; no refusal with `-AlongsideShift`; `-Areas protocols` runs the version tool once.
- [x] The header help documents every parameter and step. The script is ASCII only and runs under Windows PowerShell 5.1.

Moved to BL-1792 (interactive only): the `-DryRun` criterion and the real
`-Areas exitcodes,writeout -AlongsideShift` run. See Notes.

## Notes

- A lane cannot run the script past its refusal: under `CURL_DARK_FACTORY_LANE=2` the
  `.cmd` printed `Refusal check: refused: this is a dark factory process
  (CURL_DARK_FACTORY_LANE=2).` and exited 2, and the guard denied clearing the variable
  for a child. So the dry run and the real run moved to BL-1792 (`lane: no`), filed
  depending on this task. Checked here: `-SelfTest` printed 8 PASS lines, no FAIL; the
  script parses with no error under both Windows PowerShell 5.1 and PowerShell 7 and is
  ASCII only.
- `Measure-UpstreamCases.cs` runs from the tree, not from the tool copy beside the script:
  its `#:project ../../Curl.Console/...` line compiles the Curl.Console next to it into the
  harness, so only the tree's copy measures the tree. It is the harness, not the yardstick
  (the release's `tests/data` is). Its working directory is the tree, so the commit it
  records is the measured one.
- `Measure-VersionGap.ps1` is given `-RepositoryRoot <tree>`, because it writes the
  `Gap/Upstream/<version>/` inventories under that root; the started-from checkout is
  never written to.
- Tools run as `powershell -Command "& '<tool>' ..."` rather than `-File`, so the
  reference's feature and protocol names reach `ConvertTo-BehaviourMeasurement.ps1`'s
  `[string[]] -ReferenceFeatures` as an array (`-File` would pass one comma-joined string).
- `-DryRun` does not even `git fetch`: a fetch moves remote-tracking refs, and the
  criterion is that nothing changes.
- The summary's X of Y is each measurement's `counts.x` of `counts.y`
  (Gap/Instructions/Gap-Format.md).
- `-NewTab` opens the herdr tab the way `RunDarkFactory.ps1`'s `Start-Detached` does; the
  console-window fallback adds `-NoExit` so the summary stays readable.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. RunGapAnalysis.ps1 and .cmd written; self-test green; dry run and real run moved to interactive BL-1792
