---
id: BL-3181
title: Reuse the SMTP connection between URLs as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary/SmtpProtocolHandler.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpSession.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpKeptConnection.cs, Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerTwoUrlTests.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpControlChannel.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpCommandTransfer.cs, Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerSessionTests.cs, Curl.Console.UnitTests/CurlCompositionTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3181 — Reuse the SMTP connection between URLs as curl 8.22.0 does

## Goal

`curl -T m smtp://h/ -T m smtp://h/` sends one `EHLO`, the two `MAIL FROM`/`RCPT TO`/`DATA` envelopes and one `QUIT` on one connection, as curl 8.22.0 does.

## Context

Found by BL-3167. Measured 2026-10-22 with `Record-CurlExchange.ps1 -Smtp`, curl 8.22.0 (Schannel) running `-sS --mail-from a@x --mail-rcpt b@x -T mail.txt smtp://127.0.0.1:18118/ -T mail.txt smtp://127.0.0.1:18118/`: `EHLO mail.txt`, `MAIL FROM:<a@x> SIZE=3`, `RCPT TO:<b@x>`, `DATA`, body, `.`, then the second `MAIL FROM`, `RCPT TO`, `DATA`, body, `.`, and one `QUIT`. Curl's SMTP handler opens and quits a connection per URL. Give it the shape `Curl.Protocol.Pop3.UnitLibrary/Pop3KeptConnection.cs` and `Pop3ProtocolHandler.cs` have (BL-3167): set `PoolScheme`, hold a kept session on the connection after a success, send `QUIT` from its `ShutDownAsync`, reuse only for the same login and TLS level. `Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerTwoUrlTests.cs` already holds the measured log in an `[Ignore]`d test.

## Acceptance criteria

- [x] The `[Ignore]` on `SmtpProtocolHandlerTwoUrlTests.ExecuteAsync_TwoUploadsToTheSameServer_SendEhloAndQuitOnceAsCurlDoes` is removed and the test passes.
- [x] A test shows a different user opens a second connection, and a failed first transfer leaves no kept connection.
- [x] `dotnet build` is clean and the fast tests of `Curl.Protocol.Smtp.UnitTests` pass; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports no member outside a gate.

## Notes

- Added to touches (no task in Doing on origin/work/dark-factory names them): SmtpControlChannel.cs (QUIT becomes QuitOrKeepAsync), SmtpMailTransaction.cs and SmtpCommandTransfer.cs (call it with the result), SmtpProtocolHandlerSessionTests.cs (ConnectTarget now carries PoolScheme) and CurlCompositionTests.cs (smtp and smtps rows now pool as "smtp" and "smtps"), as BL-3167 did for POP3.
- Shape: SmtpKeptConnection mirrors Pop3KeptConnection and also carries the EHLO capabilities and AUTH result, so the second MAIL FROM still sends SIZE= and AUTH= as advertised. Reuse key is user, password and TLS level, as for POP3. A connection STARTTLS replaced is never kept (TryKeep checks the channel still talks on the pooled connection). Only a success (exit 0) keeps the connection; every failure sends QUIT at once as before.
- Measured: Curl.Protocol.Smtp.UnitTests 382 passed; whole fast suite green; Measure-CodeQuality for Curl.Protocol.Smtp.UnitLibrary first flagged OpenOrResumeAsync at 83% branch for a null-kept branch, removed by making the kept session a required constructor argument (only the handler builds a session).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 31cd83eb b29df304 8d230ff5.
- 2026-10-22: Doing -> Done. Two SMTP uploads for the same login share one connection: one EHLO, both envelopes, one QUIT, as curl 8.22.0
