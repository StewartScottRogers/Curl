---
id: BL-1450
title: Report a peer's reset as curl's 'Connection was reset', not 'Connection was aborted', on Windows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1449]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-04
completed:
---
# BL-1450 — Report a peer's reset as curl's 'Connection was reset', not 'Connection was aborted', on Windows

## Goal

On Windows, a server that resets the connection right after accepting it makes Curl report what curl 8.21.0's Schannel build reports - `curl: (56) Recv failure: Connection was reset` (WSAECONNRESET, 10054) - instead of `curl: (56) Recv failure: Connection was aborted` (WSAECONNABORTED, 10053), for every protocol that reads through `Curl.Networking`'s TCP connection.

## Context

- Measured 2026-10-04 on Windows with `Record-CurlExchange.ps1 -Reset` (the recorder closes each accepted socket with zero linger, so Windows sends an RST) and `-CurlArgs "-sS,-m,5,-u,a:b,<url>"`: real curl 8.21.0 prints `curl: (56) Recv failure: Connection was reset` for `http://`, `dict://`, `gopher://`, `mqtt://` and `ws://` URLs; `Curl.Console` prints `curl: (56) Recv failure: Connection was aborted` for every one of them, every time (four reruns of `http://` and `dict://` on 2026-10-04 agreed; an `https://` URL gives `curl: (35) Recv failure: Connection was aborted` in both, since the TLS client hello is sent first). (`ftp`, `smtp`, `imap` and `pop3` print `response reading failed (errno: 0)` in both; `telnet` exits 0 in both.)
- The wording comes from `Curl.Protocol.Abstractions.UnitLibrary/CurlSocketErrorText.cs` (BL-1325), which maps the `SocketError` it is given; the difference is in the error Curl receives. WSAECONNABORTED is what Windows reports for a socket the local stack already aborted - for instance when an earlier send on it failed after the RST arrived, or when the read is cancelled and retried - whereas curl's `recv` sees the RST itself. Start in `Curl.Networking.UnitLibrary/StreamConnection.cs` (`ReadAsync`, line 36) and `TcpConnector`, and find which operation first sees WSAECONNRESET and why it is not the one reported.
- Upstream (tag `curl-8_21_0`): `lib/cf-socket.c` `cf_socket_recv` reports the `SOCKERRNO` of the failing `recv` through `Curl_strerror`, giving `Recv failure: Connection was reset` on the Schannel build.
- Linux and macOS are not measured here; on those platforms keep whatever the existing platform tests pin. Depends on BL-1449 only because both change `Curl.Networking.UnitLibrary`.

## Acceptance criteria

- [ ] The cause is written in Notes (which call first sees the reset, and what turned it into WSAECONNABORTED).
- [ ] A test in `Curl.Networking.UnitTests` pins the fix at the seam where it is made (for example a fake socket whose first send or receive fails with `SocketError.ConnectionReset`), asserting the read failure carries `SocketError.ConnectionReset`; it runs on every platform.
- [ ] Re-running the five measured URLs against a rebuilt `Curl.Console` with `Record-CurlExchange.ps1 -Reset` prints `curl: (56) Recv failure: Connection was reset` for each (record the output in Notes).
- [ ] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean; `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
