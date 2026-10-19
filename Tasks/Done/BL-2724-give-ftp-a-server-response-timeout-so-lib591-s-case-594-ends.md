---
id: BL-2724
title: Give FTP a server-response timeout so lib591's case 594 ends with exit 28
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2622]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests/ForwardingTransferContext.cs, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed: 2026-10-19
---
# BL-2724 — Give FTP a server-response timeout so lib591's case 594 ends with exit 28

## Goal

An FTP transfer whose server never answers a command (upstream case 594: `NODATACONN` with no `150` to an active-mode `STOR`) ends with curl's timeout exit code instead of waiting forever, and case 594 passes through the runner within its 20-second limit.

## Context

Split from BL-2622, which made case 593 pass by honouring `CURLOPT_ACCEPTTIMEOUT_MS` (`ITransferContext.FtpAcceptTimeout`). `FtpSession` (`Curl.Protocol.Ftp.UnitLibrary`) has no server-response timeout: no `CURLOPT_SERVER_RESPONSE_TIMEOUT`/`--ftp-response-timeout` reaches it (`EasyNetworkOptions.ServerResponseTimeout` exists, unused by the engine), so a reply that never comes hangs the transfer.

Through the runner on Windows (BL-2622, after 593 was fixed) 594 still reports `test594 fails: curl did not finish within 20 seconds`: no differing lines, as Curl never ends the wait for `STOR`'s reply.

Measured with real curl 8.22.0 (Schannel, the tool, easy interface, default 60 s accept timeout) through `Record-CurlExchange.ps1 -Ftp -FtpIdleMilliseconds 200000 -FtpReply 'STOR=STALL'` and `-P - -T file`: curl gives up after 120093 ms with `curl: (12) Accept timeout occurred while waiting server connect`, without sending QUIT. Upstream's case 594 (lib591: multi handle, `CURLOPT_ACCEPTTIMEOUT_MS` = `%FTPTIME2` = 8 s in the runner, loop on `curl_multi_timeout`) expects exit 28 and the protocol to end with `STOR 594` then `QUIT`. Work out from upstream curl's `lib/ftp.c`/`lib/pingpong.c`/`lib/multi.c` (8.21/8.22) which timer ends the multi-driven wait with 28 and after how long, before pinning anything.

## Acceptance criteria

- [x] An FTP command whose reply never comes ends the transfer with the exit code and message real curl gives, after the time real curl waits, with a focused test in `Curl.Protocol.Ftp.UnitTests` using a fake `TimeProvider`.
- [x] Upstream case 594 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or is filed again with its differing lines.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Upstream 8.22.0 source: an unanswered FTP command ends in `Curl_pp_statemach` (`lib/pingpong.c`) once `Curl_pp_state_timeleft_ms` runs out: `data->set.server_response_timeout`, else `PINGPONG_TIMEOUT_MS` (60 s), counted from the command (`pp->response`), capped by `CURLOPT_TIMEOUT`; `failf("server response timeout")`, `CURLE_OPERATION_TIMEDOUT`. The accept timer (`CURLOPT_ACCEPTTIMEOUT_MS`, `cf_tcp_accept_timeleft`) only runs once `STOR` is answered, so lib591's 8 s does not end 594: upstream's own 594 waits 60 s.
- Measured with real curl 8.22.0 (Schannel) through `Record-CurlExchange.ps1 -Ftp`: passive `-T` with `STOR=STALL` ends after 60715 ms, `curl: (28) server response timeout`, no `QUIT`; `USER=STALL` with no `--connect-timeout` ends after 60630 ms the same way, so the login is held to 60 s, not the 300 s connect timeout. (BL-2622's 120 s and exit 12 for active mode were not reproduced here; the passive measurement is the one pinned.)
- Change: `ITransferContext.FtpServerResponseTimeout` (`TransferContext.DefaultFtpServerResponseTimeout`, 60 s), set by `TransferContextFactory` from `EasyNetworkOptions.ServerResponseTimeout`, zero meaning not set. `FtpSession.ReadReplyWithinServerResponseTimeoutAsync` races every reply read against that wait on `context.TimeProvider` without cancelling the read (a cancelled read would replay buffered bytes), waits out a timer that fires early, and ends with exit 28 `server response timeout` and no `QUIT`.
- Tests: `FtpProtocolHandlerTimeLimitTests.ExecuteAsync_StorUnansweredFor60Seconds_EndsWithExit28ServerResponseTimeoutAndNoQuit`, `..._UserUnansweredWithoutConnectTimeout_EndsWithExit28ServerResponseTimeoutAfter60Seconds`, `..._ServerResponseTimeoutSet_EndsTheUnansweredUserAfterThatTimeout`, `TransferContextFactoryTests.Create_NetworkOptionsServerResponseTimeout_IsTheContextsFtpServerResponseTimeoutWithZeroAsSixtySeconds`. The two `..._HoldsTheLoginToCurls300Seconds` tests now set a 10-minute response timeout (as `CURLOPT_SERVER_RESPONSE_TIMEOUT` 600 would), since by default curl ends that stall at 60 s; their 300 s assertions are unchanged.
- Case 594 through the runner (`CURL_UPSTREAM_CASE=594`): still `test594 fails: curl did not finish within 20 seconds`, now because Curl waits upstream's 60 s and the runner's `TimeLimit` is 20 s. Filed again as BL-2795 with that line, and with upstream's expected `QUIT`, which real curl does not send after this timeout.
- Touches widened to `Curl.Protocol.Abstractions.UnitTests/ForwardingTransferContext.cs` (it implements `ITransferContext`): no task in Doing on origin/work/dark-factory named it.
- Measure-CodeQuality for Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Abstractions.UnitLibrary and Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-18: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: e99e5beb e1300242 4272a6b9.
- 2026-10-19: Doing -> Done. An unanswered FTP command ends after curl's 60 s server response timeout (CURLOPT_SERVER_RESPONSE_TIMEOUT) with exit 28 'server response timeout' and no QUIT; case 594 refiled as BL-2795
