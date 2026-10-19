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
completed:
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

- [ ] Upstream case 594 passes through the runner on Windows and is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, and the fast suite's run time does not grow by its 60-second wait.
- [ ] Whatever decides whether FTP sends `QUIT` after a server response timeout on the multi interface has a focused test in `Curl.Protocol.Ftp.UnitTests` or the engine's tests, matching upstream 8.22.0's source.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 693d4d4d 1567690e 4272a6b9.
