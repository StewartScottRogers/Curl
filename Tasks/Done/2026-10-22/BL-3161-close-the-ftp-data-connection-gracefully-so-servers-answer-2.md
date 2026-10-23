---
id: BL-3161
title: Close the FTP data connection gracefully so servers answer 226, not 426
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerGracefulDataCloseTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3161 — Close the FTP data connection gracefully so servers answer 226, not 426

## Goal

An FTP (and FTPS) download, upload or listing from a real server ends with exit 0 and no stall, because the data connection is closed gracefully before the final reply is read.

## Context

Stewart reported on 2026-10-22: `curl -u demo:password ftp://test.rebex.net/readme.txt -o out.bin`. Measured with the preview build (0.2.0-rolling.180): Curl exits 18 with `curl: (18) server did not report OK, got 426` after 17 s with 379 bytes received; Windows System32 curl 8.21.0 Schannel exits 0 in 2 s with 379 bytes. `ftps://` is reported to fail the same way. The server's 426 text is "unable to close data connection gracefully", so Curl most likely closes the data socket with a reset, or too late. The target is curl 8.22.0; check upstream's ftp.c (`ftp_done`: the data connection is closed before the final 226 is read) and measure the target with `Record-CurlExchange.ps1` if the order of events matters.

Start in `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`: the download path disposes the data connection and then awaits the final reply (see `DisposeIfOpenAsync(dataConnection)` near line 925, the upload `data.DisposeAsync()` near line 1181 and the end-of-transfer code near line 2088). The send side must be shut down (FIN) and the connection closed before the reply is read, without leaving unread bytes (which makes the OS send RST). Handlers never build sockets (`IConnection`), so the test uses the fake connections in `Curl.Protocol.Ftp.UnitTests/Fakes/`; if `IConnection` has no way to shut down the send side, file that as a follow-up task rather than widening this one.

## Acceptance criteria

- [x] `Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerGracefulDataCloseTests.cs` has a fake FTP server that answers `426` when the data connection is disposed without its send side first being shut down (a reset) and `226` after a graceful close; a RETR download then returns `CurlExitCode.Ok` with all bytes and waits on no timeout (fake `TimeProvider`, no sleeping).
- [x] The same test class covers an upload (STOR) and a `LIST` listing, both returning `Ok` against that server.
- [x] The same test class covers the FTPS (TLS data connection) case if the handler has a separate close path for it; otherwise a comment in the test says the path is shared.
- [x] `dotnet build Curl.Protocol.Ftp.UnitLibrary -warnaserror` is clean and `dotnet test Curl.Protocol.Ftp.UnitTests --filter "TestCategory!=Integration"` passes with the existing FTP tests unchanged.
- [x] No test needs `TestCategory=Integration`, and none uses a Windows-only path or text.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 0527dce9 eff28298 bca41a23 81c923d1 cdfdbbfe.
- 2026-10-22: Doing -> Done. Data connection closed before the end-of-transfer reply; download, LIST and upload pass against a server that waits for the close.
