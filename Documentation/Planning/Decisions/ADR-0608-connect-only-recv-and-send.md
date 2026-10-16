# ADR-0608 — `CURLOPT_CONNECT_ONLY` 1, `curl_easy_recv` and `curl_easy_send`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2423.

## Context

ADR-0512 put `IConnectOnlyConnection` (`ReceiveAsync`, `SendAsync`, answering a `CurlConnectOnlyResult`)
on `IEasyTransfer.Connection`, and `EasyNetworkOptions.ConnectOnly` on the options; the managed engine's
`Connection` threw. libcurl's connect-only perform stops once the connection (and its TLS handshake) is
up, keeps it with the handle, and `curl_easy_recv` and `curl_easy_send` then move raw bytes on it without
blocking: `CURLE_AGAIN` when the socket has nothing ready or takes nothing, `CURLE_OK` with 0 bytes once
the peer has closed, and `CURLE_UNSUPPORTED_PROTOCOL` from `easy_connection` when the handle is not set
for connect-only or has no connection ("CONNECT_ONLY is required", "Failed to get recent socket"). The
engine's connections are `IConnection`s, whose reads and writes are awaitable but have no non-blocking
probe.

## Decision

1. **The perform.** With `ConnectOnly.Connect` set, `ManagedEasyTransfer.PerformAsync` parses the URL as
   any perform does, then connects to its host and port (the scheme's default when none is written), with
   TLS for the schemes libcurl's `PROTOPT_SSL` handlers serve (`https`, `ftps`, `gophers`, `imaps`,
   `ldaps`, `mqtts`, `pop3s`, `smbs`, `smtps`, `wss`), sends nothing, and keeps the connection. A failed
   connect answers the connector's `CurlCode` and text. The connector is the `TcpConnector` of the
   transfer's transports (`ManagedCurlEngine.CreateConnectOnlyConnector`), not the pool, so the
   connection is the handle's own and nothing else reuses it.
2. **Its life.** Every later perform of the handle, and cleanup, closes it; `curl_easy_reset` keeps it,
   as libcurl keeps connections, but a reset clears `CONNECT_ONLY`, so the calls then answer
   `UNSUPPORTED_PROTOCOL` as libcurl's do. `curl_easy_duphandle` carries no connection.
3. **Never blocking.** `ConnectOnlyConnection.ReceiveAsync` starts one read and answers `Again` while it
   runs; once it completes its bytes are handed over, any that do not fit kept for the next receive.
   `SendAsync` hands the bytes to one write-and-flush and answers them all sent, as a kernel buffer takes
   them, and `Again` while that write still runs; a write that fails answers `SendError`, on the call or
   the next one. A read that fails answers `RecvError`. The call's own token is checked on entry only,
   since no call waits.
4. **Wrong state.** No connection, or options no longer asking for connect-only (any non-`Disabled`
   value, as libcurl tests `connect_only` for non-zero), answer `UnsupportedProtocol` with 0 bytes.
   libcurl's `failf` text for it is not written to the error buffer: `Libcurl` fills that buffer after a
   perform only.
5. **Libcurl.** `CurlEasy.Recv` and `Send` keep libcurl's out parameter (`*n`, 0 unless `Ok`) and block on
   `RecvAsync` and `SendAsync`, which pass straight to the engine; a cleaned-up handle throws
   `ObjectDisposedException` as every `CurlEasy` member does.

Not in this decision, left to their own tasks: a connect-only perform through a proxy (no `ConnectTarget.Proxy`
is set yet; BL-2472) and `ConnectOnly.WebSocket` (2), the WebSocket upgrade, which is BL-2443's.

## Consequences

Applications using `curl_easy_recv` and `curl_easy_send` poll as they do with libcurl, waiting between
`Again`s (libcurl's examples wait on `CURLINFO_ACTIVESOCKET`, which stays -1 here: ADR-0549); a short wait
and retry works the same.
