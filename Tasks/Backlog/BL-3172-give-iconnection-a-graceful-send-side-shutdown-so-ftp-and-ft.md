---
id: BL-3172
title: Give IConnection a graceful send-side shutdown so FTP and FTPS data connections end with FIN and close_notify
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/IConnection.cs, Curl.Networking.UnitLibrary/DialedTcpConnection.cs, Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3172 — Give IConnection a graceful send-side shutdown so FTP and FTPS data connections end with FIN and close_notify

## Goal

Closing an FTP or FTPS data connection sends TLS close_notify (on a TLS data connection), shuts the TCP send side down (FIN) and never resets the connection over unread bytes, as curl does before it reads the end-of-transfer reply.

## Context

Follow-up of BL-3161, which moved the data-connection close ahead of the end-of-transfer reply in `FtpSession.ReadTransferCompleteAsync` (the test.rebex.net 426). `IConnection` has only `DisposeAsync`: a TLS data connection is disposed without `close_notify`, and a socket closed with bytes still unread (a server's `close_notify` after the last data byte, when the download stops at the `SIZE` count) makes the OS send RST, which servers such as Rebex report as 426 "unable to close data connection gracefully". Measure curl 8.22.0 against an FTPS server with `Record-CurlExchange.ps1 -Ftp -Tls` before pinning the order. Start at `Curl.Protocol.Abstractions.UnitLibrary/IConnection.cs` (a default-implemented shutdown member, as `ClearTlsAsync` is) and `Curl.Networking.UnitLibrary/DialedTcpConnection.cs`.

## Acceptance criteria

- [ ] `IConnection` has a default-implemented member that ends the send side gracefully, and the TCP and TLS connections implement it (TLS: close_notify, then TCP shutdown of the send side).
- [ ] `FtpSession` calls it on the data connection before disposing it, ahead of the end-of-transfer reply, with a unit test in `Curl.Protocol.Ftp.UnitTests`.
- [ ] The fast tests pass and every changed library meets the quality gates.

## Notes

## Log

- 2026-10-22: Created.
