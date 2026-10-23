---
id: BL-3167
title: Reuse the POP3 connection between URLs and check IMAP and SMTP do the same
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary/Pop3ProtocolHandler.cs, Curl.Protocol.Pop3.UnitLibrary/Pop3Session.cs, Curl.Protocol.Pop3.UnitLibrary/Pop3KeptConnection.cs, Curl.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerKeptConnectionTests.cs, Curl.Protocol.Imap.UnitTests/ImapProtocolHandlerTwoUrlTests.cs, Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerTwoUrlTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3167 — Reuse the POP3 connection between URLs and check IMAP and SMTP do the same

## Goal

`curl pop3://h/1 pop3://h/2` logs in once, sends `RETR 1` and `RETR 2` on the same connection and quits once, as curl does.

## Context

Stewart reported on 2026-10-22: `curl -s pop3://h/1 pop3://h/2`. Upstream sends `CAPA`, `RETR 1`, `RETR 2`, `QUIT` on one connection (measure curl 8.22.0 with `Record-CurlExchange.ps1` for the exact command log, including the login commands with credentials and whether `CAPA` is repeated); Curl logs in and quits twice. The FTP and IMAP handlers already keep and reuse the connection: read `Curl.Protocol.Ftp.UnitLibrary/FtpKeptConnection.cs` and `Curl.Protocol.Imap.UnitLibrary/ImapKeptConnection.cs` and their handlers, and give `Curl.Protocol.Pop3.UnitLibrary` the same shape (a new `Pop3KeptConnection.cs`, `Pop3ProtocolHandler.cs` reusing it, `Pop3Session.cs` sending `QUIT` only when the connection is not kept; line 139 and `QuitAsync` near 335 are where it quits today). Reuse must follow curl's rules (same host, port, user and TLS settings; a different user or `--no-keepalive`/fresh-connect options open a new one). The IMAP and SMTP part of this task is a check only: add the two-URL tests, and if either handler does not match curl 8.22.0's command log, do not widen this task, file a follow-up task for that handler.

## Acceptance criteria

- [ ] `Curl.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerKeptConnectionTests.cs` has a mock POP3 server whose command log for two URLs on one handler equals curl 8.22.0's measured log (one login, `RETR 1`, `RETR 2`, one `QUIT`), with the measurement noted in the test.
- [ ] A test shows a second URL with a different user, or with fresh-connect, opens a second connection and logs in again.
- [ ] A test shows a failed first transfer leaves no half-used kept connection for the second URL.
- [ ] `Curl.Protocol.Imap.UnitTests/ImapProtocolHandlerTwoUrlTests.cs` and `Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerTwoUrlTests.cs` each check the command log for two URLs against curl 8.22.0's measured log; a mismatch is filed as a follow-up task and named in Notes.
- [ ] `dotnet build Curl.Protocol.Pop3.UnitLibrary -warnaserror` is clean and the fast tests of `Curl.Protocol.Pop3.UnitTests`, `Curl.Protocol.Imap.UnitTests` and `Curl.Protocol.Smtp.UnitTests` pass.

## Notes

## Log

- 2026-10-22: Created.
