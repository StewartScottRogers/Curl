---
id: BL-3872
title: Echo curl's failf message as a -v line for the CGR-088 failure paths BL-3617 left: --max-redirs, --max-filesize, bad gzip, bad chunk, -m, bad -o path, short body, FTP STOR/REST/greeting/226-451, SOCKS5 replies, --trace
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/Cgr088FollowUpTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3872 — Echo curl's failf message as a -v line for the CGR-088 failure paths BL-3617 left: --max-redirs, --max-filesize, bad gzip, bad chunk, -m, bad -o path, short body, FTP STOR/REST/greeting/226-451, SOCKS5 replies, --trace

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

## Log

- 2026-10-26: Created.
