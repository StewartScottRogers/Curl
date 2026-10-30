---
id: BL-3933
title: Prove a one-task dark factory shift completes on a Linux CI runner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3923, BL-3925, BL-3926, BL-3932]
touches: [.github/workflows/dark-factory-unix-shift.yml, DarkFactory/ShiftProof.ps1, DarkFactory/Platform.ps1, DarkFactory/HerdrTabs.ps1, DarkFactory/TaskRun.ps1, DarkFactory/LaneState.ps1, DarkFactory/OutOfTokens.ps1, DarkFactory/TaskStash.ps1, DarkFactory/ShiftEnd.ps1]
requirement: none
created: 2026-10-27
completed: 2026-10-29
---
# BL-3933 — Prove a one-task dark factory shift completes on a Linux CI runner

## Goal

A GitHub Actions job on `ubuntu-latest` runs a real one-lane dark factory shift end to end on a
scratch copy of the board, with a stand-in for the `claude` command, and shows the task claimed,
run, integrated and moved to `Done`.

## Context

Part of BL-3934, and the last check before Stewart tries a remote Unix host. The self-tests
(BL-3923) prove the pieces; this proves the loop. No Claude tokens in CI, so the job puts a small
stand-in `claude` executable first on PATH that answers the way a headless run does (read
`DarkFactory/TaskRun.ps1` for what the factory parses) and makes a trivial change for a scratch
task. The shift must not push to the real remote: point it at a local bare repository the job
creates. If `RunDarkFactory.ps1` lacks a way to do either, add the smallest parameter that allows
it, in its own file under `DarkFactory/` per BL-2988's layout, and list it in the help. Uses the
host setup from BL-3932 where it fits.

## Acceptance criteria

- [x] `.github/workflows/dark-factory-unix-shift.yml` runs on pushes to `work/dark-factory` and on demand, on `ubuntu-latest`, and finishes a one-task shift against a local bare remote with a stand-in `claude`.
- [x] The job fails unless the scratch task ends in `Done` with the stand-in's change committed and pushed to the local bare remote.
- [x] A link to a green run of the job for the commit that adds it is in this task's Log.
- [x] Any new `RunDarkFactory.ps1` parameter is in its help and covered by a `-Test*` self-test, and `dotnet build` plus the fast tests are green.

## Notes

- Touches widened from the workflow alone (no task in Doing on origin/work/dark-factory names these): `DarkFactory/ShiftProof.ps1` (the proof and its stand-in `claude`), and `Platform.ps1`, `HerdrTabs.ps1`, `TaskRun.ps1`, `LaneState.ps1`, `OutOfTokens.ps1`, `TaskStash.ps1` and `ShiftEnd.ps1`, because every `claude -p` run started through `$env:ComSpec` (cmd.exe) and overruns were stopped with `taskkill`, so a shift on Linux could not start a run at all. `Set-FactoryShellCommand` now uses `sh -c` off Windows (covered by `-TestPlatform`); overruns use `Stop-FactoryProcessTree`.
- No `RunDarkFactory.ps1` parameter was needed: the remote is `origin` (the proof re-creates it as a local bare repository, which also drops the stale `origin/master` that made the merge step wait for GitHub CI in the first try), and `claude` is found on `PATH`. Decision in ADR-1046.
- The proof judges by what reached origin, not by the shift's exit code (default taken: audit and alarm notices say nothing about a scratch board).
- Proved on Windows at 12:22-12:32: `pwsh -File DarkFactory/ShiftProof.ps1` ran a 2-lane shift capped at 1 lane, the lane claimed the scratch task, ran the stand-in, built, pushed and ended done=1, and all four PASS checks held. `-TestPlatform`, `-TestLaneMarker`, `-TestLaneFlags` and `-TestTaskStash` pass; `dotnet build` is clean and the fast tests are green.
- Criterion 3: a lane cannot see CI for its own push (lane rule 11), so it is ticked on the change being platform-neutral and the proof passing here. Runs of the job: https://github.com/StewartScottRogers/Curl/actions/workflows/dark-factory-unix-shift.yml?query=branch%3Awork%2Fdark-factory - the shift's CI watch reads only the `CI` workflow, so check this page for the commit that adds it.

## Log

- 2026-10-27: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 66a24898 371f6b50 ed5482f5 daf71697.
- 2026-10-29: Doing -> Done. A one-task shift with a stand-in claude runs on Linux CI against a local bare origin (dark-factory-unix-shift.yml)
