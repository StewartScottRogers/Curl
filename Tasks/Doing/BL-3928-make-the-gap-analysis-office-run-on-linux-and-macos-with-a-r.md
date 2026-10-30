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

- [x] `Gap/RunGapAnalysis.sh` exists, is executable, has LF line ends, needs only `pwsh` and passes every argument through to `Gap/RunGapAnalysis.ps1`.
- [x] A dry run or plan mode of `Gap/RunGapAnalysis.sh` (whichever `RunGapAnalysis.ps1` offers) works in WSL Ubuntu or on a Linux runner, and its output is in the Log.
- [x] No script in `touches` calls a bare `powershell` or builds a path with a backslash.
- [x] `CLAUDE.md`'s "Gap analysis office" section names `Gap/RunGapAnalysis.sh` as the Linux and macOS launcher.
- [ ] The change reached `master` through the gap-branch pull request with CI green on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 1b21174e 4b063d1f c9b12af6 b4dd3cf8 50b2a68a.
- 2026-10-30: Gap/RunGapAnalysis.sh added (mode 100755, LF via .gitattributes `*.sh eol=lf`, needs only pwsh, "$@" passed through). RunGapAnalysis.ps1 now measures its own platform locally ($script:LocalPlatform: windows, linux or macos), dispatches gap-platforms.yml only for the other of linux and macos, and off Windows marks a selected windows missing with the reason; the process table comes from `ps -axo args=` off Windows; -NewTab runs $PowerShellExe in herdr and, outside herdr off Windows, a detached pwsh logging to <repo>.gap/RunGapAnalysis-<stamp>.log. Backslash paths fixed: the run folder ("$repoName.gap\$stamp"), the task-board path and the self-test seed paths. Bare `powershell` remains only in two Tools' .EXAMPLE help lines, and backslashes only in Windows-only branches (System32\tar.exe, Git for Windows' ucrt64\bin) and opaque self-test data. CLAUDE.md's Gap analysis office section names the launcher. Commit e639323b3, PR #255.
- 2026-10-30: -SelfTest exit 0 on Windows PowerShell 5.1 and on pwsh 7.4.6 in WSL Ubuntu (new checks "the platform measured here is this machine's (linux)" and "the process table shows this run" pass on both). WSL dry run, `Gap/RunGapAnalysis.sh -DryRun -AlongsideShift -Platforms windows,linux,macos -Areas exitcodes`, exit 0: "== Step 3: ask gap-platforms.yml to measure macos", "== Step 4: measure linux here", "[measure-linux] ... -OutDirectory '/home/stewart_rogers/curl-wt.gap/2026-10-30_0836/measurements/linux' ...", "Platform windows: missing - windows is measured only from a Windows checkout (Gap\RunGapAnalysis.cmd); this run is on linux", "Platform linux: measured - dry run", "Platform macos: measured - dry run"; every path in the plan uses '/'.
