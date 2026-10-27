---
id: BL-3928
title: Make the gap analysis office run on Linux and macOS, with a RunGapAnalysis.sh launcher
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [CLAUDE.md, Gap/RunGapAnalysis.ps1, Gap/RunGapAnalysis.sh, Gap/Tools]
lane: no
requirement: none
created: 2026-10-27
completed:
---
# BL-3928 — Make the gap analysis office run on Linux and macOS, with a RunGapAnalysis.sh launcher

## Goal

`Gap/RunGapAnalysis.sh` runs a gap analysis on Linux and macOS under `pwsh`, measuring that
platform locally the way `Gap\RunGapAnalysis.cmd` measures Windows.

## Context

Part of BL-3934 (the whole repository workable on Linux and macOS). Interactive only: `Gap/` is a
gap office path, so the work goes through the `gap` branch and is merged under the standing
exception. `gap-platforms.yml` already measures Linux and macOS in CI with curl built from the
tarball (ADR-0505), so the Tools largely run there; find what only works locally on Windows.
Files with Windows-only constructs found by grep: `Gap/RunGapAnalysis.ps1`,
`Gap/Tools/Export-GapDashboardData.ps1`, `Gap/Tools/Get-UpstreamRelease.ps1`,
`Gap/Tools/Measure-ReferenceCrossCheck.ps1` and `Gap/Tools/Write-GapScorecard.ps1`; check the rest
of `Gap/Tools/` too. `Gap/RunGapAnalysis.sh` is a new launcher, like `RunDarkFactory.sh`, that runs
`RunGapAnalysis.ps1` under `pwsh` with the same arguments.

## Acceptance criteria

- [ ] `Gap/RunGapAnalysis.sh` exists, is executable, has LF line ends, needs only `pwsh` and passes every argument through to `Gap/RunGapAnalysis.ps1`.
- [ ] A dry run or plan mode of `Gap/RunGapAnalysis.sh` (whichever `RunGapAnalysis.ps1` offers) works in WSL Ubuntu or on a Linux runner, and its output is in the Log.
- [ ] No script in `touches` calls a bare `powershell` or builds a path with a backslash.
- [ ] `CLAUDE.md`'s "Gap analysis office" section names `Gap/RunGapAnalysis.sh` as the Linux and macOS launcher.
- [ ] The change reached `master` through the gap-branch pull request with CI green on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-27: Created.
