---
id: BL-1031
title: Find why the CI watch filed nothing while the TcpDialerTests failures kept CI red for hours
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-29
completed:
---
# BL-1031 — Find why the CI watch filed nothing while the TcpDialerTests failures kept CI red for hours

## Goal

When a CI run on the shift's branch fails, the running shift's coordinator files a High task for each failing test within one heartbeat, as BL-987 intended, and logs that it did.

## Context

Shift 20260929-191003 ran with BL-987's `Invoke-CiWatch` in its coordinator loop (`RunDarkFactory.ps1`, called beside `Publish-BoardStatusIfDue`). From 04:24 UTC on 2026-09-30 every `CI` run on `work/dark-factory` failed on ubuntu-latest with `BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext` and `BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed` (e.g. run 36674691490), yet the coordinator log `Curl.logs/DarkFactory-20260929-191003.log` has no CI watch line at all and no task was filed; the shift's end logged `merge   not merged: CI failure on bfd523f`. An interactive session found and fixed the failure (BL-1030). `-TestCiWatch` passes on its recorded logs, so the fault is in the live path: reading `gh run list`/`gh run view --log-failed`, the run selection, a silent catch, or the loop never reaching the call.

## Acceptance criteria

- [ ] The cause is found and stated under Notes.
- [ ] `Invoke-CiWatch` writes one coordinator log line each time it examines a finished run (run id, conclusion, what it filed or why it filed nothing).
- [ ] Replayed against run 36674691490 (or a recorded copy of its `--log-failed` output), the watch files one task per failing test.
- [ ] `-TestCiWatch` covers the case that failed live, and every `-Test*` switch prints no FAIL.

## Notes

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
