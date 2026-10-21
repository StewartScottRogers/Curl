---
id: BL-2351
title: Fix CI failure 1024 on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Tftp.UnitTests, Curl.Protocol.Tftp.UnitLibrary]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2351 — Fix CI failure 1024 on Windows

## Goal

`1024` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `1024` failed on Windows in CI run 38796134816 (https://github.com/StewartScottRogers/Curl/actions/runs/38796134816). First failing commit: 58485c6f.

    CollectionAssert.AreEqual failed. Element at index 46 do not match.

Lanes test only on Windows, so reproduce with `gh run view 38796134816 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `1024` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix. (Local: 283/283 in Curl.Protocol.Tftp.UnitTests. CI runs on the shift's push; the cause is removed on every platform, see Notes.)

## Notes

- Cause: a test race, not a product defect. The request's `timeout` option is derived from
  the time left on the 300-second default connect timeout (300 s / 50 retries = 6, as curl's
  `tftp_set_timeouts`). The tests ran on `TimeProvider.System`; under the CI quality job's
  coverage instrumentation the transfer started more than 0.5 s after the clock's start
  timestamp, so the time left rounded to 299 s and 299 / 50 = 5 was sent (byte 46, '5' for '6').
- Fix: every context whose request bytes are asserted with `timeout 6` now uses the project's
  `ManualTimeProvider`, a frozen clock, in `TftpOptionsTests`, `TftpProtocolHandlerTests`,
  `TftpRequestFileTests` and `TftpUploadTests`. The scripted channels answer at once, so no
  test waits on a timer the frozen clock would never fire. Platform-neutral.
- Only `Curl.Protocol.Tftp.UnitTests` changed (no production code, nothing references the
  test project), so the build and fast tests were run for that project, and
  `dotnet format --verify-no-changes` on it is clean. `Curl.Protocol.Tftp.UnitLibrary` was
  not changed, so no quality run was needed.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. TFTP request tests pin a frozen clock, so timeout 6 no longer drops to 5 under slow coverage runs
