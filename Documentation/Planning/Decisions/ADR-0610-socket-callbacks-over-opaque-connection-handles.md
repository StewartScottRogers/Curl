# ADR-0610 — The socket callbacks run around each engine connection, over opaque `CurlSocket` handles

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2418.

## Context

`Curl.Api`'s `EasyCallbackOptions` carries `CURLOPT_OPENSOCKETFUNCTION`, `CURLOPT_SOCKOPTFUNCTION` and
`CURLOPT_CLOSESOCKETFUNCTION` (`OpenSocketCallback`, `SocketOptionCallback`, `CloseSocketCallback`), whose
sockets are `CurlSocket`, an opaque number (ADR-0506). Nothing called them, and `Libcurl` answered
`CURLE_NOT_BUILT_IN` for all six options.

In curl 8.22.0 `cf_socket_open` asks the open-socket callback for a socket (with the `struct curl_sockaddr` it
will connect to), fails that attempt with `CURLE_COULDNT_CONNECT` on `CURL_SOCKET_BAD`, then calls the
socket-option callback, failing with `CURLE_ABORTED_BY_CALLBACK` (and closing the socket) on
`CURL_SOCKOPT_ERROR` and skipping the connect on `CURL_SOCKOPT_ALREADY_CONNECTED`; the happy-eyeballs filter then
reports `Failed to connect to <host> port <port> after <n> ms: <curl_easy_strerror>`. `Curl_socket_close`
calls the close-socket callback in place of `close()` whenever one is set, whoever made the socket.

The managed engine never owns a socket an application could use: `Curl.Networking`'s `TcpConnector` makes,
connects and wraps it (TLS, proxies, happy eyeballs) and hands out an `IConnection`. Protocol handlers must
never see a `Socket` (root `CLAUDE.md`).

## Decision

1. `Curl.Engine`'s `SocketCallbackConnector` wraps the connector that opens a transfer's TCP connections, beneath
   the pool (`ManagedCurlEngine.PoolOf`: `PoolingConnector.Over` the wrapper, the FTP data connector too) and for
   `CURLOPT_CONNECT_ONLY`, only when a handle sets one of the three callbacks; otherwise nothing changes.
2. A socket is an opaque `CurlSocket` over the `IConnection`: the one the open-socket callback returned, else the
   engine's next number from `CurlSocketHandles` (1, 2, 3 ...; shared by an engine's handles, never static).
3. The callbacks run once the inner connector has connected, in curl's order: open-socket with
   `CURLSOCKTYPE_IPCXN` and the peer's address (`SocketAddress` bytes, `SOCK_STREAM`, `IPPROTO_TCP`, the family as
   the running platform numbers it: `AF_INET6` 23 on Windows, 30 on macOS, 10 on Linux), then socket-option, and
   close-socket once when the connection closes (the pool closing it, a refused connect, the engine's cleanup).
   `CURL_SOCKET_BAD` closes the connection and fails with exit 7; `CURL_SOCKOPT_ERROR` closes it, calls
   close-socket and fails with exit 42; each message is `Failed to connect to <host>:<port> after <n> ms: <text>`,
   the form `TcpConnector` already uses for curl 8.22.0. `CURL_SOCKOPT_ALREADY_CONNECTED` carries on as `OK`
   does, the connection being made already.
4. QUIC connections pass through without the callbacks.

## Consequences

- An application sees libcurl's call order and refusal outcomes, and a connection opened under the callbacks
  calls close-socket when it really closes, even when a later handle reused it from the pool, as curl's
  `conn->fclosesocket` does.
- The callbacks run after the TCP connect rather than before it, so a refusal is visible on the wire as a
  connection opened and closed; the callbacks cannot set real socket options or supply their own socket. A
  native engine (ADR-0506) can honour them fully; the managed one cannot without handing out its `Socket`.
- The open-socket callback for a QUIC connection (`SOCK_DGRAM`) is not called yet.
