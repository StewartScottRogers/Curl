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
completed:
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

- [ ] Real curl's answer for both command lines is recorded under Notes.
- [ ] `test1211` passes in `UpstreamConformanceTests` and is added to `PassingUpstreamCases.txt`; `test1206` and `test1207` still pass.
- [ ] The behaviour is pinned in `Curl.Protocol.Ftp.UnitTests`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-10: Created.
