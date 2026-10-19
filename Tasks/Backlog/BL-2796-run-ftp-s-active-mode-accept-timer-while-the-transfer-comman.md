---
id: BL-2796
title: Run FTP's active-mode accept timer while the transfer command waits for its reply
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2796 — Run FTP's active-mode accept timer while the transfer command waits for its reply

## Goal

An active-mode FTP upload or download whose server never answers the transfer command and never connects ends the way curl 8.22.0 does: exit 12, `Accept timeout occurred while waiting server connect`, after the accept timeout plus a 60-second wait for the control reply, with no `QUIT`.

## Context

Measured with real curl 8.22.0 (Schannel, the tool) on 2026-10-19 for BL-2795:
`Record-CurlExchange.ps1 -Port 47594 -Ftp -FtpIdleMilliseconds 150000 -FtpReply 'EPRT=500 we dont like EPRT now','STOR=STALL' -CurlArgs '-v','-P','-','-T',<file>,'ftp://127.0.0.1:47594/path/594'`:
the transcript ends at `STOR 594`, and curl exits 12 after 120128 ms with no `QUIT`. Its stderr ends with
`* FTP response timeout`, `* control connection looks dead`, `* shutting down connection #0`, and
`curl: (12) Accept timeout occurred while waiting server connect`.

In curl 8.22.0, `ftp_do_more` calls `Curl_conn_connect` on the listening secondary socket on each
pass while `STOR`'s reply is still pending. The accept filter's timer (`CURLOPT_ACCEPTTIMEOUT_MS`,
60 s by default) runs from the listen and ends the transfer with `CURLE_FTP_ACCEPT_TIMEOUT`. That
code is in `ftp_done_status`'s benign list, so `ftp_done_control_reply` then waits for the pending
reply, up to 60 s. On timeout it says `control connection looks dead` and clears `ctl_valid`, so
`ftp_disconnect` sends no `QUIT`. `lib/multi.c` keeps the 12 and drops `ftp_done`'s own result.

Curl today (`Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`): `AcceptDataConnectionAsync` starts
`FtpAcceptTimeout` only once the transfer command is answered. Before that, `ReadReplyAsync` waits
`FtpServerResponseTimeout` (BL-2724) and ends with 28, `server response timeout`, after 60 s. That
60 s wait is still right for passive mode (measured in BL-2724: `STOR=STALL` passive, 60715 ms, 28).
ADR-0706 records why upstream case 594 is not listed.

## Acceptance criteria

- [ ] In active mode, with no reply to `STOR` (or `RETR`/`LIST`) and no data connection, `FtpProtocolHandler` ends with exit 12 and `Accept timeout occurred while waiting server connect` after `FtpAcceptTimeout`, then waits up to 60 s more for the control reply, with `FTP response timeout` and `control connection looks dead` as info lines and no `QUIT`. A test in `Curl.Protocol.Ftp.UnitTests` on a fake `TimeProvider` pins this.
- [ ] If the transfer command's reply comes within the accept timeout, today's active-mode flow is unchanged (the existing active-mode tests pass).
- [ ] Passive mode keeps BL-2724's 28 after `FtpServerResponseTimeout` (existing tests pass).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-19: Created.
