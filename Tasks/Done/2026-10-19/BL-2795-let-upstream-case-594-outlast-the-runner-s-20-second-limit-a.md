---
id: BL-2795
title: Let upstream case 594 outlast the runner's 20-second limit and settle its expected QUIT
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2724]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2795 — Let upstream case 594 outlast the runner's 20-second limit and settle its expected QUIT

## Goal

Upstream case 594 (lib591, FTP active-mode `STOR` the server never answers) passes through the conformance runner without holding the fast suite for a real minute, and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

BL-2724 gave FTP curl's server response timeout: a reply that does not come within `ITransferContext.FtpServerResponseTimeout` (60 s, `CURLOPT_SERVER_RESPONSE_TIMEOUT`) ends the transfer with exit 28 and `server response timeout`, no `QUIT`, as curl 8.22.0 measured (passive `STOR=STALL`: 60715 ms; `USER=STALL`: 60630 ms). Upstream's own 594 waits that same 60 s (`lib/pingpong.c` `PINGPONG_TIMEOUT_MS`, `Curl_pp_statemach`); lib591's `CURLOPT_ACCEPTTIMEOUT_MS` (8 s) only applies once `STOR` is answered.

Through the runner on Windows after BL-2724, `CURL_UPSTREAM_CASE=594` still reports `test594 fails: curl did not finish within 20 seconds`: `UpstreamConformanceTests.TimeLimit` is 20 s of `TimeProvider.System`, and the case needs 60 s. Two things to work out:

1. How the runner can run 594 without a real 60 s wait (for example a clock the in-process libtest engine and the `ftpd` emulation share that jumps idle waits, or a per-case limit), without loosening the limit for every other case.
2. Upstream's `<protocol>` for 594 ends with `QUIT`, but real curl 8.22.0 (tool) sends no `QUIT` after `server response timeout`, and upstream's `ftp_done_status` marks the control connection invalid for exit 28, so `ftp_quit` sends none. Read `lib/multi.c`/`lib/ftp.c` 8.22.0 for the multi-interface path lib591 takes (`multi_done`, `Curl_conn_terminate`, `ftp_disconnect`) and match it, measuring before pinning.

## Acceptance criteria

- [x] Upstream case 594 stays off `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, and ADR-0706 records why: curl 8.22.0 lists it in `tests/data/DISABLED`, and its `<verify>` ends with a `QUIT` that upstream's source never sends after the timeout.
- [x] The real difference the measurement found (FTP active mode's accept timer while `STOR` is unanswered) is filed as its own task, BL-2796.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- 2026-10-19: Stewart had the first two criteria reworded per ADR-0706. Case 594 cannot pass as upstream wrote it, so the criteria now ask for what was decided and found: 594 stays off the ratchet with the reason recorded, and Curl's real difference is its own task.

- 2026-10-19 (lane 1): the first criterion cannot be met as written. No code changed.
  - curl 8.22.0's `tests/data/DISABLED` lists `594` unconditionally, so upstream never runs the case and its `<verify>` (28, then `QUIT`) is unchecked.
  - Upstream's source cannot produce that `<verify>`. `ftp_done_status` clears `ctl_valid` for 28. It also clears it for the `curl_multi_cleanup` path, `multi_done(CURLE_OK, premature TRUE)`. `ftp_quit` sends `QUIT` only while `ctl_valid` is set.
  - Real curl measured in 594's setup (`-P - -T`, `EPRT=500`, `STOR=STALL`): exit 12 after 120128 ms, no `QUIT`.
  - Decided in ADR-0706: 594 stays off the ratchet and the runner gets no special clock.
  - Curl's real difference, found by that measurement, is filed as BL-2796: in active mode the accept timer should run while `STOR` is unanswered (12 after 120 s, where Curl gives 28 after 60 s).
  - The second criterion has nothing to pin beyond BL-2724's no-`QUIT` behaviour, which already matches the source. Stewart: close this task or reword its criteria per ADR-0706.

- 2026-10-19: Checked: ADR-0706-upstream-case-594-stays-off-the-ratchet.md is on work/dark-factory; 594 is not in PassingUpstreamCases.txt; BL-2796 is filed (and already Done). This task changed no code. CI run 39269364577 on 52d4fcb50, which contains ADR-0706 (075810766), built the solution and passed the fast tests on Windows, Linux and macOS.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 693d4d4d 1567690e 4272a6b9.
- 2026-10-19: Doing -> Blocked. Stewart: case 594 is in curl 8.22.0's tests/data/DISABLED and its expected QUIT contradicts upstream's source (ADR-0706), so criterion 1 cannot be met - close or reword the task; Curl's real active-mode difference is filed as BL-2796
- 2026-10-19: Blocked -> Backlog. Stewart: reword the criteria per ADR-0706.
- 2026-10-19: Backlog -> Doing. Interactive session, with the reworded criteria. Criteria at claim: 385ced1d 57153753 4272a6b9.
- 2026-10-19: Doing -> Done. 594 stays off the ratchet per ADR-0706; the real active-mode difference was BL-2796.
