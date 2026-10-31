---
id: BL-3934
title: Make the whole repository workable on Linux and macOS
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3919, BL-3920, BL-3921, BL-3922, BL-3923, BL-3924, BL-3925, BL-3926, BL-3927, BL-3928, BL-3929, BL-3930, BL-3931, BL-3932, BL-3933]
touches: [CLAUDE.md]
requirement: none
created: 2026-10-27
completed:
---
# BL-3934 — Make the whole repository workable on Linux and macOS

## Goal

Every script in the repository can be run on Linux and macOS as well as Windows, so the whole
repository, the dark factory included, can be worked from a Unix machine; this task closes when
all the work it depends on is done and `CLAUDE.md` says so.

## Context

Stewart, 2026-10-27: "complete porting of .ps1 and .cmd to .sh so that the entire repository can
be worked with Unix, and not just Windows. In the near future I want to try running the dark
factory on a remote UNIX system rather than my desktop." He approved approach A that day: keep
one PowerShell copy of each script, make it run under PowerShell 7 (`pwsh`) on Linux and macOS,
and give each entry point a short `.sh` launcher, rather than rewriting ~25,000 lines in bash. The
root entry points already have `.sh` siblings (`hrdrClaudeNative.sh`, `RunClaude.sh`,
`RunDarkFactory.sh`, `Measure-CodeQuality.sh`, `New-DifferentialPki.sh`,
`Record-StapledOcspExchange.sh`, `Set-DifferentialEnvironment.sh`, `Update-PublicSuffixList.sh`,
`Record-CurlExchange.sh` (HTTP and HTTPS only), and `install.sh`).

This is the tracking task: its work is the tasks in `depends-on`.
- Dark factory: BL-3919 (platform seam), BL-3920 (memory and processes), BL-3921 (tabs and lanes), BL-3922 (alarm), BL-3923 (self-tests in CI), BL-3933 (a full shift on Linux CI).
- Remote host: BL-3932 (`Setup-UnixFactoryHost.sh` and the wiki page).
- Other scripts: BL-3924 (`.github` and conformance data), BL-3925 (task-admin, board self-tests, hooks).
- Interactive only, on the `audit`/`gap` branches: BL-3926 (guard files), BL-3927 (audit office), BL-3928 (gap office).
- `Record-CurlExchange.sh` remaining modes: BL-3929 (FTP), BL-3930 (mail), BL-3931 (the rest).

## Acceptance criteria

- [x] Every task in `depends-on` is in `Done` or its archive.
- [x] `git ls-files '*.cmd'` lists no `.cmd` file without a `.sh` sibling in the same folder.
- [x] `CLAUDE.md`'s "Toolchain" section says the repository is worked on Windows, Linux and macOS (PowerShell 7 required off Windows) instead of "Shell is Windows", and names the `.sh` launchers.
- [ ] `dotnet build` and the fast tests are green.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: 782fe427 00d01702 461a34c2 d265299d.
- 2026-10-30: All 15 depends-on tasks are in Done (BL-3928 today, the rest in Done/2026-10-28 and Done/2026-10-29). Every file from `git ls-files '*.cmd'` has a `.sh` beside it (the check printed none missing). CLAUDE.md's Toolchain now says the repository is worked on Windows, Linux and macOS with pwsh required off Windows and names the 13 .sh launchers; DarkFactory/LaneRules.md's "Shell is Windows" now names the host's shell too.
