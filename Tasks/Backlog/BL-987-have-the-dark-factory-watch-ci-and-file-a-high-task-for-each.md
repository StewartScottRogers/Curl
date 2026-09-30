---
id: BL-987
title: Have the dark factory watch CI and file a High task for each new Linux or macOS failure within minutes
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-29
completed:
---
# BL-987 — Have the dark factory watch CI and file a High task for each new Linux or macOS failure within minutes

## Goal

When the `CI` workflow fails on `work/dark-factory`, the running shift files a High-priority task naming the failing test, platform and run within one heartbeat, so a lane fixes it in minutes rather than the break sitting unnoticed until someone asks for status.

## Context

Lanes test only on Windows. On 2026-09-29 two Linux/macOS-only breaks went unnoticed while lanes kept pushing on top of them: the SSH RSA test (fixed by BL-905) and `FromComponents_ZeroCoefficient_Throws`, red from 20:40 to 22:06 UTC across nine runs (fixed by BL-972). A red CI blocks `Invoke-MergeToMaster` at the end of the shift.

Why the coordinator and not each lane: `.github/workflows/ci.yml` has `concurrency: ci-${{ github.ref }}` with `cancel-in-progress: true`, so with several lanes pushing most runs are cancelled and a lane waiting on "its" run usually sees `cancelled`. Only the coordinator sees the whole stream; `Invoke-MergeToMaster` (around line 1647) already reads runs with `gh run list --workflow CI`.

## Acceptance criteria

- [ ] On each heartbeat the coordinator reads the latest `CI` runs on the shift's branch that finished `success` or `failure` (ignoring `cancelled`), newest first.
- [ ] For a `failure`, it reads the failing test names and jobs from `gh run view <id> --log-failed` (lines `Failed <TestName>`, plus build `error` lines when no test failed).
- [ ] It files one High task per failing test (or per build error) with `task-board.ps1 new`, with the test name, the failing platforms, the run id, the first failing commit if found, and the error message in its Context, and `touches` set to that test's project and its library.
- [ ] It files nothing for a test that already has a live task naming it (search Backlog, Doing and Blocked for the test name), so repeated red runs file one task, not one per run.
- [ ] A test that fails in only one of the last several runs is filed as a flaky-test task with that word in the title, not as a regression.
- [ ] It writes a coordinator log line for each task filed, and the whisper hook announces it.
- [ ] Documented in the script's header and in `CLAUDE.md`'s dark factory section.

## Log

- 2026-09-29: Created.
