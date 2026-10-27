# ADR-0840 — CURLINFO_ACTIVESOCKET and the close-socket callback give the operating system socket

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3658, CGR-129).

## Context

CGR-129 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`) measured libcurl 8.22.0 (Schannel,
Windows) and 8.18.0 (OpenSSL, Linux): after `CURLOPT_CONNECT_ONLY` 1, `CURLINFO_ACTIVESOCKET` and
`CURLINFO_LASTSOCKET` answer the connection's real socket, which an application polls with
`select`; and a handle that sets only `CURLOPT_CLOSESOCKETFUNCTION` gets the real socket there, open,
so `getsockopt(SO_TYPE)` on it succeeds and the callback closes it. Curl answered engine numbers
(`0x60000001` and on from `ConnectOnlyConnection`, 1 and on from `CurlSocketHandles`, ADR-0610)
because the managed sockets lived inside `Curl.Networking`. Since ADR-0820 (BL-3846)
`IConnection.SocketHandle` gives the TCP connection's operating system handle through every wrapper.

## Decision

1. `ConnectOnlyConnection.OpenAsync` keeps the connection under its `SocketHandle`, so
   `CURLINFO_ACTIVESOCKET` and `CURLINFO_LASTSOCKET` answer the real socket. A connection with none
   (the conformance harness's in-memory connections, test fakes) still gets an engine number from
   `FirstSocket`.
2. Without a `CURLOPT_OPENSOCKETFUNCTION`, `SocketCallbackConnector` gives the close-socket callback the
   connection's `SocketHandle` once it has connected (`CloserOf`, and `OpenSocketAsync` for a connector
   that never asks the opener), else the engine's `CurlSocketHandles` number as before. The
   `CurlSocketCloser` is marked `IsTheConnectionsOwnSocket`, and `SocketCallbackConnection.DisposeAsync`
   then calls the callback before it closes the connection, so the callback sees the socket open, as
   libcurl's `Curl_socket_close` calls it in place of `close()`. A socket the open-socket callback
   supplied keeps the old order: the dialer adopted it without owning it, so the engine never closes it.
3. The engine still closes its own socket after the callback. When the callback has closed it too, that
   second close fails harmlessly (`WSAENOTSOCK`, `EBADF`); the small window in which another thread
   could have been given the same handle number in between is accepted, since .NET gives no way to
   hand a socket's ownership away, and an application that only inspects the socket would otherwise
   leak it.
4. `CURLOPT_SOCKOPTFUNCTION` still gets the engine number when no open-socket callback is set: it runs
   before the dialer creates its socket (`ITcpSocketOpener.Open`), so giving it the real socket needs
   the dialer to create the socket before asking the opener. That is filed as its own task.

## Consequences

- An application can poll the connect-only socket and inspect or close the socket in its close-socket
  callback, as with libcurl.
- In-memory connections keep the engine numbers, so the conformance ratchet's libtests see no change.
