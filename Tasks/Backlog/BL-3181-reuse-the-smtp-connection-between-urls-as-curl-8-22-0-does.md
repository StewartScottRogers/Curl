---
id: BL-3181
title: Reuse the SMTP connection between URLs as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary/SmtpProtocolHandler.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpSession.cs, Curl.Protocol.Smtp.UnitLibrary/SmtpKeptConnection.cs, Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerTwoUrlTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3181 — Reuse the SMTP connection between URLs as curl 8.22.0 does

## Goal

`curl -T m smtp://h/ -T m smtp://h/` sends one `EHLO`, the two `MAIL FROM`/`RCPT TO`/`DATA` envelopes and one `QUIT` on one connection, as curl 8.22.0 does.

## Context

Found by BL-3167. Measured 2026-10-22 with `Record-CurlExchange.ps1 -Smtp`, curl 8.22.0 (Schannel) running `-sS --mail-from a@x --mail-rcpt b@x -T mail.txt smtp://127.0.0.1:18118/ -T mail.txt smtp://127.0.0.1:18118/`: `EHLO mail.txt`, `MAIL FROM:<a@x> SIZE=3`, `RCPT TO:<b@x>`, `DATA`, body, `.`, then the second `MAIL FROM`, `RCPT TO`, `DATA`, body, `.`, and one `QUIT`. Curl's SMTP handler opens and quits a connection per URL. Give it the shape `Curl.Protocol.Pop3.UnitLibrary/Pop3KeptConnection.cs` and `Pop3ProtocolHandler.cs` have (BL-3167): set `PoolScheme`, hold a kept session on the connection after a success, send `QUIT` from its `ShutDownAsync`, reuse only for the same login and TLS level. `Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerTwoUrlTests.cs` already holds the measured log in an `[Ignore]`d test.

## Acceptance criteria

- [ ] The `[Ignore]` on `SmtpProtocolHandlerTwoUrlTests.ExecuteAsync_TwoUploadsToTheSameServer_SendEhloAndQuitOnceAsCurlDoes` is removed and the test passes.
- [ ] A test shows a different user opens a second connection, and a failed first transfer leaves no kept connection.
- [ ] `dotnet build` is clean and the fast tests of `Curl.Protocol.Smtp.UnitTests` pass; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Smtp.UnitLibrary` reports no member outside a gate.

## Notes

## Log

- 2026-10-22: Created.
