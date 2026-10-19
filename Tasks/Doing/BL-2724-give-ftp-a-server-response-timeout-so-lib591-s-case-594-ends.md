---
id: BL-2724
title: Give FTP a server-response timeout so lib591's case 594 ends with exit 28
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2622]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed:
---
# BL-2724 — Give FTP a server-response timeout so lib591's case 594 ends with exit 28

## Goal

An FTP transfer whose server never answers a command (upstream case 594: `NODATACONN` with no `150` to an active-mode `STOR`) ends with curl's timeout exit code instead of waiting forever, and case 594 passes through the runner within its 20-second limit.

## Context

Split from BL-2622, which made case 593 pass by honouring `CURLOPT_ACCEPTTIMEOUT_MS` (`ITransferContext.FtpAcceptTimeout`). `FtpSession` (`Curl.Protocol.Ftp.UnitLibrary`) has no server-response timeout: no `CURLOPT_SERVER_RESPONSE_TIMEOUT`/`--ftp-response-timeout` reaches it (`EasyNetworkOptions.ServerResponseTimeout` exists, unused by the engine), so a reply that never comes hangs the transfer.

Through the runner on Windows (BL-2622, after 593 was fixed) 594 still reports `test594 fails: curl did not finish within 20 seconds`: no differing lines, as Curl never ends the wait for `STOR`'s reply.

Measured with real curl 8.22.0 (Schannel, the tool, easy interface, default 60 s accept timeout) through `Record-CurlExchange.ps1 -Ftp -FtpIdleMilliseconds 200000 -FtpReply 'STOR=STALL'` and `-P - -T file`: curl gives up after 120093 ms with `curl: (12) Accept timeout occurred while waiting server connect`, without sending QUIT. Upstream's case 594 (lib591: multi handle, `CURLOPT_ACCEPTTIMEOUT_MS` = `%FTPTIME2` = 8 s in the runner, loop on `curl_multi_timeout`) expects exit 28 and the protocol to end with `STOR 594` then `QUIT`. Work out from upstream curl's `lib/ftp.c`/`lib/pingpong.c`/`lib/multi.c` (8.21/8.22) which timer ends the multi-driven wait with 28 and after how long, before pinning anything.

## Acceptance criteria

- [ ] An FTP command whose reply never comes ends the transfer with the exit code and message real curl gives, after the time real curl waits, with a focused test in `Curl.Protocol.Ftp.UnitTests` using a fake `TimeProvider`.
- [ ] Upstream case 594 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or is filed again with its differing lines.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: e99e5beb e1300242 4272a6b9.
