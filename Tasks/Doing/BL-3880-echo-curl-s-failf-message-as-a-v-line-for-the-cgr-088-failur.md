---
id: BL-3880
title: Echo curl's failf message as a -v line for the CGR-088 failure paths BL-3617 left: --max-redirs, --max-filesize, bad gzip, bad chunk, -m, bad -o path, short body, FTP STOR/REST/greeting/226-451, SOCKS5 replies, --trace
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3948, BL-3949, BL-3950, BL-3951, BL-3952]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/Cgr088FollowUpTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3880 — Echo curl's failf message as a -v line for the CGR-088 failure paths BL-3617 left: --max-redirs, --max-filesize, bad gzip, bad chunk, -m, bad -o path, short body, FTP STOR/REST/greeting/226-451, SOCKS5 replies, --trace

## Goal

Under `-v` and `--trace`, every failure path CGR-088 lists beyond its explicit repros writes curl 8.22.0's `failf` message as a `* <message>` info line, in curl's order, as BL-3617 did for `-f` 404, an empty reply, FTP 530/550/QUOT, CONNECT refusals and SOCKS4 rejections.

## Context

CGR-088 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (see BL-3617, Done). BL-3617 echoed the message at the failing site (`HttpProtocolHandler.FailedAttemptOutcome`'s helpers, `FtpSession.QuitAndFailAsync` and friends, `TcpConnector.TunnelFailure`, `TcpConnector.ReportSocks4Failure`). Still to do, each measured with real curl through `Record-CurlExchange.ps1` before pinning: `--max-redirs`, `--max-filesize` (HTTP), bad gzip, bad chunk, `-m`, a bad `-o` path, a short body, FTP STOR/REST errors, FTP greeting 500, 226 replaced by 451, SOCKS5 replies other than the GSS-API path (which already echoes, `ReportSocks4Failure` skips SOCKS5), and the same lines under `--trace`. Echo at the failing site in each library, not as a general echo in `CurlCommandRunner`, or the lines already written would repeat. Split by library if it is too big for one run; widen `touches` to the files named in each step (the two files here are placeholders for the console part).

## Acceptance criteria

- [ ] Each listed failure path, run through real curl 8.22.0 and through Curl with `-v`, gives the same stderr lines; commands and results recorded in Notes.
- [ ] A focused unit test in each fixed library's `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3617's run, which narrowed BL-3617 to the issue's explicit repro commands.
- 2026-10-28 (lane 4, first claim): split for size before any code, as the Context allows. BL-3617 took 80+ turns for three libraries; this list spans four (Http, Ftp, Networking, Console). Filed BL-3948 (HTTP --max-redirs, --max-filesize, -m), BL-3952 (HTTP bad gzip, bad chunk, short body; depends on BL-3948 as both edit HttpProtocolHandler.cs), BL-3949 (FTP STOR/REST/500 greeting/226-451), BL-3950 (SOCKS5 replies, Networking) and BL-3951 (bad -o path, Console), each with file-level touches and its own focused test file and the --trace check. Once they are Done, the next claim of this task only re-measures every path against real curl 8.22.0 under -v and --trace, records the commands in Notes and ticks the criteria; any gap left is a new task.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 27475e82 ee75e456 779790c0.
- 2026-10-28: Doing -> Backlog. Split for size: waits on BL-3948, BL-3949, BL-3950, BL-3951 and BL-3952, one per library
- 2026-10-28: Backlog -> Doing. Criteria at claim: 27475e82 ee75e456 779790c0.
