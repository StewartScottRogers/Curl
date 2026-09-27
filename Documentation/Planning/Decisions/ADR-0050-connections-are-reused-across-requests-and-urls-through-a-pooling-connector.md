# ADR-0050 — Connections are reused across requests and URLs through a pooling connector

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"; Phase 1
  HTTP plan, item X8, 2026-09-26; recorded by BL-164, 2026-09-27).

## Context

curl keeps a connection open after a transfer and hands it to the next transfer on the same
command line that wants the same kind of connection. Curl does not: `HttpProtocolHandler`
connects through `IConnector.ConnectAsync`, reuses the connection only for its own retries
inside one transfer (an authentication retry or a resend without `Expect`, ADR-0034), and
disposes it with `await using` when the transfer returns. `IConnector`'s contract says the
handler owns and disposes the `IConnection` it is given (ADR-0005), and `IConnection` has no
way to say "this can carry another request". So a second URL to the same host opens a
second connection, and `%{num_connects}` and `-v` differ from curl's.

The pieces already present:

- `TransferReport.ConnectionCount` is the source of `%{num_connects}`; the handler sets it
  to `1` per exchange on a new connection and `0` on one it kept
  (`HttpProtocolHandler.ExchangeAsync`, `newConnection`), and `RedirectFollower` sums it
  across hops (ADR-0015).
- `HttpConnectionPersistence.KeepsAlive` already decides, per RFC 9112 section 9.3, whether
  a response leaves its connection usable: not after `Connection: close`, not for HTTP/1.0
  without `Connection: keep-alive`, not when the body runs to close.
- ADR-0046 decided `ITransferEvents`, with a `ReportConnectionReused` event, and
  `ConnectTarget.Events`, through which a connector reports. Neither exists in code yet.
- `CurlComposition.CreateTransports` builds one `TcpConnector`, over one
  `SslStreamTlsProvider` with one `TlsClientOptions`, per command line; the parser does not
  implement `--next`, `-Z`/`--parallel` or `--no-keepalive`.

curl 8.21.0 (x86_64-w64-mingw32, Schannel) was measured on 2026-09-27 against a local
Python `ThreadingHTTPServer` speaking HTTP/1.1 with `Content-Length`, and against
`https://example.com/`:

| Command line | `-v` lines (excerpt) | `%{num_connects}` |
| --- | --- | --- |
| `curl -v http://127.0.0.1:18231/a http://127.0.0.1:18231/b` | `* Connection #0 to host 127.0.0.1:18231 left intact`, then `* Reusing existing http: connection with host 127.0.0.1`, then `left intact` again | `1`, `0` |
| `curl -v https://example.com/ https://example.com/x` | `* Reusing existing https: connection with host example.com`; `* Connection #0 to host example.com:443 left intact` | `1`, `0` |
| First response carries `Connection: close` | `* shutting down connection #0`, then `* Hostname 127.0.0.1 was found in DNS cache`, a new `Trying`, and `Connection #1` | `1`, `1` |
| First response has no length, not chunked (read to close) | `* no chunk, no close, no size. Assume close to signal end`, `* shutting down connection #0`, a new connection | `1`, `1` |
| `--max-filesize 3` against a 6-byte body (exit 63) | `* Maximum file size exceeded`, `* closing connection #0`, a new connection for the next URL | `1`, `1` |
| `http://127.0.0.1:18231/a` then `http://localhost:18231/b` | No reuse: the host name differs | `1`, `1` |
| `-x http://127.0.0.1:18231 http://a.example/x http://b.example/y` | `* Reusing existing http: connection with proxy 127.0.0.1` - a forward proxy connection serves both origins | `1`, `0` |
| `--no-keepalive` with two URLs to one host | Reuses exactly as without it | `1`, `0` |
| Eleven ports in turn, then the first again | From the sixth: `* Connection pool is full, closing the oldest of 6/5` and `* shutting down connection #0`, printed after the body and before `left intact`; the last URL does not find port 18231's connection | all `1` |
| A reused connection, `-w` connect times | `time_namelookup`, `time_connect` and `time_appconnect` print `0.000000`; `local_port` and `remote_ip` are the first transfer's | `0` |

The reuse line in curl 8.21.0 is `Reusing existing <scheme>: connection with host|proxy
<name>`, not the older `Re-using existing connection` wording; the name is the URL's (or
proxy's) host without the port. See https://curl.se/docs/manpage.html#-w for
`num_connects`, https://curl.se/libcurl/c/CURLMOPT_MAXCONNECTS.html for the cache size,
https://curl.se/libcurl/c/CURLOPT_MAXAGE_CONN.html for the idle limit and
https://curl.se/docs/manpage.html#--no-keepalive for TCP keepalive (all checked against
curl 8.21.0).

## Decision

### A pooling connector owns the pool, one per command line

`Curl.Networking.UnitLibrary` gains `PoolingConnector`, an `IConnector` and
`IAsyncDisposable` that wraps the `TcpConnector`. `CurlComposition.CreateTransports` builds
one per run and hands it to every handler in place of the `TcpConnector`, so the pool lives
exactly as long as curl's connection cache does: one command-line invocation. Disposing it
at the end of the run closes every idle connection without writing anything, as curl's
`-v` shows nothing after the last `left intact`.

`ConnectAsync` looks for an idle connection with the target's key. When it finds one it
takes it out of the pool and returns it; otherwise it calls the inner connector. Either way
it returns a `PooledConnection`, an `IConnection` that forwards every read and write to the
underlying connection. An idle connection is never handed to two transfers, because a
leased connection is not in the pool.

### How a handler hands a connection back

`IConnection` gains one member, `void MarkReusable()`, with a default implementation that
does nothing. The handler calls it once the response has been read to its end and
`HttpConnectionPersistence.KeepsAlive` says the connection persists, then disposes the
connection exactly as today. `PooledConnection.DisposeAsync` returns the underlying
connection to the pool when it was marked, and disposes it otherwise.

So the ownership rule of ADR-0005 stands: the handler still disposes what it is given, and
a handler that never calls `MarkReusable` - every protocol but HTTP, and every error path in
HTTP - still closes its connection. Reuse is opt-in and the safe outcome is the default.

### The pool key

A connection is reused only for a target equal to the one it was opened for in every one
of these:

| Part | Where it comes from |
| --- | --- |
| Scheme | New `ConnectTarget.PoolScheme` (`string?`, `init`, default `null`). `null` means the connection is never pooled and never served from the pool, so only a handler that sets it takes part. HTTP sets `http` or `https`. |
| Host | `ConnectTarget.Host`, compared ordinally ignoring case, as given - `localhost` and `127.0.0.1` are different keys, as measured. |
| Port | `ConnectTarget.Port`. |
| TLS | `ConnectTarget.UseTls`, and the `TlsClientOptions` the inner connector's `SslStreamTlsProvider` holds: verification (`-k`), minimum version, CA file and directory, client certificate, key, key and certificate types, passphrase, and the cipher lists. Today that is one record per run, so one `PoolingConnector` per run keys it implicitly. When `--next` or a pinned public key (`--pinnedpubkey`) lets it vary within a run, the `TlsClientOptions` record, compared by value, joins the key. |
| Proxy | `ConnectTarget.Proxy`: kind, host, port and credential, the credential compared by user name, password and domain rather than by `NetworkCredential` reference. A tunnel therefore serves only its own origin. |

A forward proxy (an `http` URL through an HTTP proxy without `-p`) is connected with the
proxy itself as `Host` and `Port` and no `Proxy` (`HttpProtocolHandler.TargetOf`), so one
proxy connection serves every origin behind it, which is what curl does. The proxy
credential need not be in that key: forward-proxy authentication is a per-request
`Proxy-Authorization` header. Origin credentials are not in the key either, because Basic,
Bearer and Digest are sent per request; a connection-bound scheme such as NTLM or Negotiate
must add them when it is implemented.

### When a connection is not reusable

The handler does not call `MarkReusable`, so the connection is closed, when:

- the response names `Connection: close`;
- the response is HTTP/1.0 without `Connection: keep-alive`;
- the body runs to close: not chunked and no `Content-Length`, or `--raw` or
  `--ignore-content-length` makes it so (all three already decided by
  `HttpConnectionPersistence.KeepsAlive`);
- the transfer fails or times out at any point, including a local refusal such as
  `--max-filesize` (exit 63) or a failed write to the output;
- the body was not read to its end for any other reason;
- the response switches protocols (101).

The pool also drops an idle connection whose idle time exceeds 118 seconds, curl's
`CURLOPT_MAXAGE_CONN` default, measured on the injected `TimeProvider`. A reused connection
that fails before any response byte arrives is the handler's to retry once on a fresh
connection, as curl does for a connection that died while idle; the text curl prints for
that is measured by the task that implements it.

### The limit

The pool holds at most five idle connections in total, across all hosts, the number curl
8.21.0 printed (`6/5`). When a sixth is returned, the oldest idle connection is closed. The
limit is not per host because curl's is not. Five is a constant, not an option: curl's tool
has no command-line option for it.

### `%{num_connects}` and `-v`

`ConnectResult` gains `bool IsReused` and `long ConnectionNumber`, set through the
`Connected` factory. The pool numbers connections from `0` in the order the inner connector
opens them, as curl's `#N` does, and returns a reused connection's result with
`IsReused = true`, its original `ConnectionNumber` and `LocalEndPoint`, and no `Timings`, so
the connect times print `0.000000` as measured. `TcpConnector` on its own leaves both at
their defaults, `false` and `0`.

- **`%{num_connects}`.** The handler passes `newConnection: !connect.IsReused` where it
  passes `true` today, so a transfer on a pooled connection reports `ConnectionCount` `0`
  and `RedirectFollower` still sums hops.
- **`* Reusing existing <scheme>: connection with host|proxy <name>`.** The pool reports it,
  through ADR-0046's `ReportConnectionReused` on `ConnectTarget.Events`, before it returns.
  That event's payload as ADR-0046 lists it (`HostName`, `Port`, `ConnectionNumber`) cannot
  produce the measured text, so this ADR adds `Scheme` and `IsProxy` to
  `ConnectionReusedEvent`; `Curl.Output`'s formatter (BL-228) owns the wording.
- **The end-of-transfer line.** The handler writes it as `ReportInfo` text, because only it
  knows why the transfer ended: `Connection #<N> to host <host>:<port> left intact` when it
  marks the connection reusable, `shutting down connection #<N>` when the response did not
  persist, and `closing connection #<N>` when the transfer failed, with `<N>` from
  `ConnectResult.ConnectionNumber` and host and port from the target it connected to.
- **Eviction.** The pool writes `Connection pool is full, closing the oldest of <n>/5` and
  `shutting down connection #<N>` through the `Events` of the target whose connection is
  being returned, before the handler's `left intact` line - so `PooledConnection` keeps its
  target's `Events`, and the handler disposes the connection before writing that line.

### Options that interact

- **`--no-keepalive`** turns off TCP keepalive probes (`SO_KEEPALIVE`) and nothing else; it
  never disables pooling, as measured. It is not parsed today.
- **`-Z`/`--parallel`** is not implemented. The pool is safe under concurrent calls, since a
  leased connection is out of the pool; it does not mirror curl's parallel wait-for-a-
  pending-connection logic, and curl 8.21.0 itself opened a second connection for three
  parallel URLs to one host. No multiplexing exists to share a connection, as there is no
  HTTP/2 (ADR-0017).
- **`--connect-timeout`** and the `HttpTransferDeadline` apply to `ConnectAsync` as today;
  a reuse returns at once.

### Who does what

BL-215 builds `PoolingConnector`, `PooledConnection`, the key and the limit in
`Curl.Networking.UnitLibrary`. The new `IConnection.MarkReusable`, `ConnectTarget.PoolScheme`,
`ConnectResult.IsReused` and `ConnectResult.ConnectionNumber` in
`Curl.Protocol.Abstractions.UnitLibrary`, the handler's use of them, and the composition
change in `Curl.Console` lie outside BL-215's `touches` and need their own tasks.

## Consequences

- A second URL, a redirect hop or a retry to the same key costs no connect or handshake,
  and `%{num_connects}`, `-v` and the connect times match curl's.
- Every protocol but HTTP is untouched: it never sets `PoolScheme` and never calls
  `MarkReusable`, and the default implementation means no existing `IConnection` fake has
  to change.
- The default hides a wrapper that forgets to forward `MarkReusable`. That is safe - the
  connection is closed, never wrongly reused - but only a test on the connection the
  handler actually marks (the outermost one it was given) catches it.
- A reused connection can be dead. The retry-once rule covers it, at the cost of one more
  code path in the handler.
- The key is implicit in one respect (TLS options, one set per run) and must become
  explicit when `--next` arrives.

## Alternatives considered

- **No reuse; open a connection per URL.** Simplest, but `%{num_connects}` and `-v` differ
  from curl on every multi-URL command line, and scripts that count connections would
  notice. Rejected: not a drop-in replacement.
- **The handler keeps its own pool.** It would reuse only within HTTP's own lifetime, key
  nothing TLS or proxy related that the connector owns, and move connection lifetime into a
  protocol library, against ADR-0005. Rejected.
- **A separate `ReleaseAsync(IConnection, bool reusable)` on `IConnector`.** Explicit, but
  every connector and fake must implement it, and a handler that forgets it leaks a
  connection instead of closing it. Rejected for `MarkReusable` plus the existing dispose,
  where forgetting is safe.
- **`MarkReusable` with no default implementation.** Matches ADR-0045 and ADR-0046, but
  forces a no-op into about twenty `IConnection` implementations across eleven projects for a
  member only one class acts on, and for a non-pooled connection "do nothing" is the correct
  behaviour, not a hidden gap. Rejected.
- **Key on the resolved address.** curl keys on the name, and `localhost` against
  `127.0.0.1` measured as two connections. Rejected.
- **A per-host limit, or no limit.** curl's measured limit is five in total; a per-host limit
  would keep connections curl closes, and no limit holds sockets open for the whole run.
  Rejected.
