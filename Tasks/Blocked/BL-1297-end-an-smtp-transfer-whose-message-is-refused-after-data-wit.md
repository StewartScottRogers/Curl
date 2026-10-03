---
id: BL-1297
title: End an SMTP transfer whose message is refused after DATA with curl's -v lines
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1297 — End an SMTP transfer whose message is refused after DATA with curl's -v lines

## Goal

When the server answers the end of an SMTP message with anything but 250, Curl's `-v` output ends as curl 8.21.0's does: no `Weird server reply` line and `Connection #0 to host H:P left intact`, with exit 8 and `curl: (8) Weird server reply` unchanged.

## Context

- curl 8.21.0 `lib/smtp.c` (tag `curl-8_21_0`) `smtp_state_postdata_resp`, lines 1489-1505: a code other than 250 sets `CURLE_WEIRD_SERVER_REPLY` with no `failf`, so nothing is written under `-v` and the exit text comes from curl_easy_strerror; the DONE phase then ends normally, so the connection is kept and `-v` ends with `Connection #0 to host H:P left intact`. `QUIT` is still sent when the handle is cleaned up, after the `-v` output ends.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Smtp -SmtpReply 'DATADONE=554 rejected' -StandardInput 'hi\r\n'` and `-sv --mail-from a@b --mail-rcpt c@d -T - smtp://127.0.0.1:PORT/`: exit 8; stderr ends
  ```
  } [4 bytes data]
  * upload completely sent off: 7 bytes
  < 554 rejected
  * Connection #0 to host 127.0.0.1:PORT left intact
  ```
  and the transcript ends `> QUIT`, `< 221 Bye`. Curl today ends `* Weird server reply` then `* shutting down connection #0` (its QUIT and transcript match). Without `-v` (`-S -s`) both print `curl: (8) Weird server reply` and exit 8.
- Curl today: `Curl.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs` returns `TransferResult.Failure(CurlExitCode.WeirdServerReply, SmtpSessionMessages.WeirdServerReply)` for that reply (around line 213), and `SmtpProtocolHandler.ReportConnectionEnd` writes every failure's message except `Login denied` and `Failed sending data to the peer`, then `shutting down connection #N` once `QUIT` was sent. The other `Weird server reply` failures (`SmtpCommandTransfer`, `SmtpSession` greeting and NUL-byte cases) do call `failf` in curl and keep their lines; only the end-of-data case changes.
- The `--trace-config smtp` end lines (`channel.Trace.Ended`) keep their place relative to the connection line.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Smtp.UnitTests` drives the measured exchange through the fake connection and asserts exit 8 (`CurlExitCode.WeirdServerReply`), message `Weird server reply`, info lines ending `upload completely sent off: 7 bytes` then `Connection #0 to host 127.0.0.1:<port> left intact` with no `Weird server reply` line, and `QUIT` sent.
- [ ] A test pins that a 552 to `MAIL FROM` still ends with exit 55, the `MAIL failed: 552` line and the connection line it ends with today (measured identical to curl on 2026-10-02), so only the end-of-data reply changes.
- [ ] A test pins that a reply with a NUL byte after the message still ends with its own message line, as today.
- [ ] `dotnet build Curl.Protocol.Smtp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Smtp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Blocked. Stewart: dark factory timed out after 120 min; see Z:\repos\Curl.logs\BL-1297-20261002-211047-L1.jsonl
