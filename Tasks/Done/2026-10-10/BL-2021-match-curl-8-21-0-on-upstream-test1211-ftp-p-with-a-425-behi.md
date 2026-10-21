---
id: BL-2021
title: Match curl 8.21.0 on upstream test1211: FTP -P with a 425 behind the 150 and no --max-time ends with exit 28 and no QUIT
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2021 — Match curl 8.21.0 on upstream test1211: FTP -P with a 425 behind the 150 and no --max-time ends with exit 28 and no QUIT

## Goal

Upstream test1211 (`ftp://%HOSTIP:%FTPPORT/1211 -P -`, server `NODATACONN425`) passes through Curl: exit 28 and no `QUIT` after `RETR`, as curl 8.21.0 ends it, so gap finding GF-0048's item `behaviour:test1211` measures `match`.

## Context

- Split out of BL-1978. BL-1978 made a 4xx/5xx reply already read behind the transfer command's `150` end the active-mode accept wait with exit 10 after `QUIT` (`FtpSession.AcceptDataConnectionAsync`, `FtpControlChannel.HasBufferedNegativeReply`), as upstream tests 1206 and 1207 (the same server with `--max-time %FTPTIME2`) expect; both pass and are listed.
- test1211 is the same exchange without `--max-time`, and upstream expects exit 28 with no `QUIT`. Curl now gives exit 10 and `QUIT` there (`<verify><protocol> differs at byte 83 (line 8): expected the end, got "QUIT\r\n"`); before BL-1978 it waited the full 60-second accept timeout.
- Why curl 8.21.0 answers 28 without `--max-time` and 10 with it is not yet known. Start by measuring real curl: `Record-CurlExchange.ps1 -Ftp` with `-FtpReply 'RETR=150 Opening data connection'` followed by a `425 Can't open data connection` in the same write, and with the recorder told not to dial back for `EPRT` (extend it if it cannot), once with `--max-time 8` and once without. Read curl 8.21.0's `lib/ftp.c` (`ReceivedServerConnect`, `ftp_timeleft_accept`, `ftp_do_more`) beside it.
- Run the case: `dotnet test Curl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCase_RunThroughCurl_HoldsTheRatchet"` and read `test1211`'s row.

## Acceptance criteria

- [x] Real curl's answer for both command lines is recorded under Notes.
- [x] ~~`test1211` passes in `UpstreamConformanceTests` and is added to `PassingUpstreamCases.txt`~~ Superseded by ADR-0474: real curl contradicts test1211, so it is measured `excluded`, not listed; `test1206` and `test1207` still pass.
- [x] The behaviour is pinned in `Curl.Protocol.Ftp.UnitTests`.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Measured real curl 8.21.0 (Schannel, x86_64-w64-mingw32) with `Record-CurlExchange.ps1 -Ftp -FtpIdleMilliseconds 120000 -FtpReply "RETR=150 Opening data connection\r\n425 Can't open data connection"` (an overridden RETR opens no data connection, and EPRT is answered 200 without a dial-back, so the recorder needed no extension):
  - `--max-time 8 ftp://127.0.0.1:<port>/1211 -P - -v`: exit 10, `QUIT` sent after `RETR`, 0.5 s.
  - `ftp://127.0.0.1:<port>/1211 -P - -v`: exit 10, `QUIT` sent after `RETR`, 0.1 s.
  - Both print `Ctrl conn has data while waiting for data conn`, `FTP code: 425`, `curl: (10) FTP: The server failed to connect to data port`.
- curl 8.21.0's `lib/ftp.c`: `ftp_check_ctrl_on_data_wait` returns `CURLE_FTP_ACCEPT_FAILED` for any reply above 3xx read during the data-connection wait, independent of `--max-time`; exit 28 there only comes from `ftp_readresp`'s 421 handling.
- curl 8.21.0's `tests/data/DISABLED` lists 1211 (with 1209, after "test 1184 causes flakiness in CI builds"): upstream never runs it, so its exit 28 is unverified.
- Decision (ADR-0474): keep exit 10 + QUIT, as real curl does; do not list test1211; the gap office measures `behaviour:test1211` as `excluded` with that reason. Applying it under `Gap/` is left to an interactive session (lanes may not touch `Gap/`).
- Pinned already by `FtpProtocolHandlerActiveModeTests.ExecuteAsync_NegativeReplyBehindThe150_QuitsWithExit10WithoutWaiting` (no transfer time set, i.e. test1211's command line); its comment now names test1211 and ADR-0474.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. Real curl 8.21.0 measured: test1211's exchange exits 10 after QUIT with or without --max-time; Curl matches, and ADR-0474 measures the upstream-disabled test1211 excluded
