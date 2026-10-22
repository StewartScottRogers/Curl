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
completed: 2026-10-07
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

- [x] The cause is written in Notes (which call first sees the reset, and what turned it into WSAECONNABORTED).
- [x] A test in `Curl.Networking.UnitTests` pins the fix at the seam where it is made (for example a fake socket whose first send or receive fails with `SocketError.ConnectionReset`), asserting the read failure carries `SocketError.ConnectionReset`; it runs on every platform.
- [x] Re-running the five measured URLs against a rebuilt `Curl.Console` with `Record-CurlExchange.ps1 -Reset` prints `curl: (56) Recv failure: Connection was reset` for each (record the output in Notes).
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean; `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Notes

- Cause (measured 2026-10-07, a temporary trace in `StreamConnection.ReadAsync` and a .NET client
  run as the "curl" under `Record-CurlExchange.ps1 -Reset`): the request's write succeeds and the
  RST arrives after it; the first call to see the reset is the first `NetworkStream.ReadAsync`
  (`Socket.ReceiveAsync`), which fails at once with 10053. Nothing in Curl turns it into
  WSAECONNABORTED: Windows itself answers a receive issued more than a few milliseconds after the
  RST arrived with WSAECONNABORTED, and one issued sooner or already pending with WSAECONNRESET.
  Overlapped, blocking, non-blocking, after-`Poll` and zero-byte receives all behave alike, and
  `SO_ERROR` reads 0. A 30 ms gap between write and read gave 10053 in 8 of 8 runs; a read posted
  before the write gave 10054. Real curl's `recv` runs inside the window: 9 of 10 interleaved runs
  printed the reset (5 of 8 back-to-back runs printed the abort, so curl races too). Curl's first
  read comes later and printed the abort 10 of 10.
- Fix (ADR-0419): on Windows `StreamConnection.ReadAsync` reports a read failing with
  `ConnectionAborted` as an `IOException` carrying `ConnectionReset` (`ReportsAbortedReadAsReset`,
  internal init property, so `StreamConnectionTests` pins both settings on every platform).
  Reading earlier (a read posted before the request) was rejected: it reorders every protocol's
  reads and returned 0 bytes after an early RST, which would be exit 52.
- Re-measured with the rebuilt Debug `Curl.Console`, `-sS,-m,5,-u,a:b,<scheme>://127.0.0.1:47814/x`,
  three runs each: `http`, `dict`, `gopher`, `mqtt`, `ws` all printed
  `curl: (56) Recv failure: Connection was reset`, exit 56 (15 of 15).
- Gates: build `-warnaserror` clean; `Curl.Networking.UnitTests` fast run 3032 passed, 0 failed,
  28 skipped; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. On Windows a peer's reset is reported as curl's 'Recv failure: Connection was reset' for http, dict, gopher, mqtt and ws
