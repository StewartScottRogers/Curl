---
id: BL-1055
title: Write Measure-Performance.ps1's transfer output into each run's folder, not the process working directory
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Tools/Measure-Performance.ps1]
lane: no
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1055 — Write Measure-Performance.ps1's transfer output into each run's folder, not the process working directory

## Goal

`Measure-Performance.ps1` writes each transfer's output inside that run's folder, never into the directory it was started from.

## Context

Found on 2026-09-30 while delivering BL-1008: a stray `out.bin` holding `ok` (the redirects scenario's final body) appeared in the audit worktree's root, written at 15:30 during the BL-1007 run. `Invoke-Run` used `Push-Location` into the run's folder and passed `-o out.bin`, but `Push-Location` changes PowerShell's location, not the process's working directory that `Record-CurlExchange.ps1` starts curl in, so every scenario's output, the 50 MiB body among them, went to the starting directory, and the clean-up looked in the wrong place. The timings are unaffected: curl wrote the same bytes, only elsewhere.

## Acceptance criteria

- [x] Each scenario's `-o` gets a full path in its run's folder, and that file is removed after the run.
- [x] A real redirects run through the fixed `Invoke-Run` exits 0 and leaves no `out.bin` in the working directory.
- [x] The script still parses and is ASCII only.

## Notes

- Fixed on the audit branch in the commit after 2d42e80c, in pull request https://github.com/StewartScottRogers/Curl/pull/36 (with BL-1008). `-o` takes the placeholder `OUTFILE`, replaced with `<run folder>\out.bin`; the empty `Push-Location`/`Pop-Location` pair is gone.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Measure-Performance.ps1 writes each run's output into its own folder; in PR #36, awaiting Stewart's merge.
