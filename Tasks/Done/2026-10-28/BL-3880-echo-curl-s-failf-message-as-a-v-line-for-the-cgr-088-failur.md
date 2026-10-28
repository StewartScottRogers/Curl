---
id: BL-3880
title: Echo curl's failf message as a -v line for the CGR-088 failure paths BL-3617 left: --max-redirs, --max-filesize, bad gzip, bad chunk, -m, bad -o path, short body, FTP STOR/REST/greeting/226-451, SOCKS5 replies, --trace
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3948, BL-3949, BL-3950, BL-3951, BL-3952, BL-3975]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/Cgr088FollowUpTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3880 — Echo curl's failf message as a -v line for the CGR-088 failure paths BL-3617 left: --max-redirs, --max-filesize, bad gzip, bad chunk, -m, bad -o path, short body, FTP STOR/REST/greeting/226-451, SOCKS5 replies, --trace

## Goal

Under `-v` and `--trace`, every failure path CGR-088 lists beyond its explicit repros writes curl 8.22.0's `failf` message as a `* <message>` info line, in curl's order, as BL-3617 did for `-f` 404, an empty reply, FTP 530/550/QUOT, CONNECT refusals and SOCKS4 rejections.

## Context

CGR-088 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (see BL-3617, Done). BL-3617 echoed the message at the failing site (`HttpProtocolHandler.FailedAttemptOutcome`'s helpers, `FtpSession.QuitAndFailAsync` and friends, `TcpConnector.TunnelFailure`, `TcpConnector.ReportSocks4Failure`). Still to do, each measured with real curl through `Record-CurlExchange.ps1` before pinning: `--max-redirs`, `--max-filesize` (HTTP), bad gzip, bad chunk, `-m`, a bad `-o` path, a short body, FTP STOR/REST errors, FTP greeting 500, 226 replaced by 451, SOCKS5 replies other than the GSS-API path (which already echoes, `ReportSocks4Failure` skips SOCKS5), and the same lines under `--trace`. Echo at the failing site in each library, not as a general echo in `CurlCommandRunner`, or the lines already written would repeat. Split by library if it is too big for one run; widen `touches` to the files named in each step (the two files here are placeholders for the console part).

## Acceptance criteria

- [x] Each listed failure path, run through real curl 8.22.0 and through Curl with `-v`, gives the same stderr lines; commands and results recorded in Notes.
- [x] A focused unit test in each fixed library's `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3617's run, which narrowed BL-3617 to the issue's explicit repro commands.
- 2026-10-28 (lane 4, first claim): split for size before any code, as the Context allows. BL-3617 took 80+ turns for three libraries; this list spans four (Http, Ftp, Networking, Console). Filed BL-3948 (HTTP --max-redirs, --max-filesize, -m), BL-3952 (HTTP bad gzip, bad chunk, short body; depends on BL-3948 as both edit HttpProtocolHandler.cs), BL-3949 (FTP STOR/REST/500 greeting/226-451), BL-3950 (SOCKS5 replies, Networking) and BL-3951 (bad -o path, Console), each with file-level touches and its own focused test file and the --trace check. Once they are Done, the next claim of this task only re-measures every path against real curl 8.22.0 under -v and --trace, records the commands in Notes and ticks the criteria; any gap left is a new task.
- 2026-10-28 (lane 4, second claim): re-measured end to end against real curl 8.22.0 (the Git for Windows debug Schannel build) and Curl (`dotnet run --no-build --project Curl.Console --`, passed as `-Curl dotnet` to `Record-CurlExchange.ps1`), each under `-sv` and `-s --trace-ascii %`. Compared the `*`/`== Info:` lines, ignoring the reference's debug-only lines (`Curl_xfer...`, `sendrecv_dl...`, and the DEBUGF `RANGE 3 to end of file` / `range-download from ...` lines that curl_range.c writes only in debug builds) and normalising ports. Same lines and exit code under both modes: `--max-filesize 5` against Content-Length 100 (63, `Maximum file size exceeded`); `-L --max-redirs 0` on a 302 (47, `Maximum (0) redirects followed`); `--compressed` with a bad gzip body (61); a `zz` chunk size (56, both chunk lines); Content-Length 100 with 5 bytes (18, `end of response with 95 bytes missing`); FTP `-T` with `STOR=550 no` (25); FTP `-C 3` with `REST=500 no` (31, `Could not use REST`, `shutting down connection #0`); `GREETING=500 no` (8); `RETRDONE=451 aborted` (18); `-o nodir/x.txt file:///.../s.txt` (23, `client returned ERROR on write of 12 bytes`). SOCKS5 replies: taken from BL-3950's measurement against a loopback stand-in (same info event, so `--trace` shows it too); not re-run, because the recorder has no SOCKS5 mode.
- Gap found: `-m 1` with 5 of 100 bytes and the server holding the connection open (`-HoldOpenMilliseconds 3000`): real curl writes `* Operation timed out after N milliseconds with 5 out of 100 bytes received` once, but Curl writes it twice. `HttpProtocolHandler.OutcomeOfTimeout` calls `ReportInfo(message)` twice (c44f09cc4). Filed BL-3975 to fix it. BL-3975 waits on BL-3874, which holds HttpProtocolHandler.cs. Requeued on BL-3975: once it is Done, re-run only the `-m` command (in BL-3975's Context), then tick the criteria. ADR-0482's focused tests are already in each library's Cgr088 test file (BL-3948 to BL-3952, BL-3955) and in BL-3975.

- 2026-10-28 (lane 11, third claim): BL-3975 is Done. Re-ran only the `-m` command: `Record-CurlExchange.ps1 -OutDirectory <dir> -Port 47311 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\nhello' -HoldOpenMilliseconds 3000 -CurlArgs -sv,-m,1,http://127.0.0.1:47311/` (and `-s,--trace-ascii,%`), then the same with `-Curl dotnet -CurlArgs run,--no-build,--project,Curl.Console,--,...`. Both write `* Operation timed out after N milliseconds with 5 out of 100 bytes received` once, then `* closing connection #0`, exit 28, under both modes; the only stderr differences are the debug-build-only lines (`!!! WARNING !!!`, `sending last upload chunk`) and ports. This run changed no production code, so no Measure-CodeQuality run was needed (BL-3975 ran it for Curl.Protocol.Http.UnitLibrary). The focused tests (ADR-0482) are in each library's Cgr088 test file, from BL-3948 to BL-3952 and BL-3975. `dotnet build` is clean (0 warnings) and the fast tests pass in all 46 test projects. They are platform-neutral, so CI on Linux and macOS is left to the shift's CI watch.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 27475e82 ee75e456 779790c0.
- 2026-10-28: Doing -> Backlog. Split for size: waits on BL-3948, BL-3949, BL-3950, BL-3951 and BL-3952, one per library
- 2026-10-28: Backlog -> Doing. Criteria at claim: 27475e82 ee75e456 779790c0.
- 2026-10-28: Doing -> Backlog. Waits on BL-3975 (-m writes its timeout line twice; HttpProtocolHandler.cs, held by BL-3874); every other path re-measured and matches
- 2026-10-28: Backlog -> Doing. Criteria at claim: 27475e82 ee75e456 779790c0.
- 2026-10-28: Doing -> Done. every CGR-088 failure path BL-3617 left echoes curl's failf line under -v and --trace, re-measured against curl 8.22.0
