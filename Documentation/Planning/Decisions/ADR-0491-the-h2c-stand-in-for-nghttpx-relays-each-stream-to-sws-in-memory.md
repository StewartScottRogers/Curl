# ADR-0491: The h2c stand-in for nghttpx relays each stream to sws in memory

- Status: Accepted
- Date: 2026-10-14
- Decided by Claude under Stewart's delegation (BL-2244)

## Context

Upstream's `http/2` test server is nghttpx terminating cleartext HTTP/2 (h2c) and speaking
HTTP/1.1 to `sws` (`tests/servers.pm` at curl-8_22_0). The case runner needs a stand-in on
`%HTTP2PORT` (BL-2230), built like the SSH stand-in (ADR-0456): an `IConnector` whose
connections are in-memory pipes. Upstream's cases pin what nghttpx does: test1700's
`<protocol>` shows the HTTP/1.1 request `sws` receives (`Host` first, curl's fields with
capitalised names, `X-Forwarded-Proto` and `Via` stripped by `<strip>`), and its `<stdout>`
shows the reply as nghttpx turns it round (`connection` gone, `server: nghttpx` dropped by
`<stripfile>`, `via: 1.1 nghttpx` last).

## Decision

`Curl.Conformance.Http2Server.UnitLibrary` holds the stand-in; `UpstreamHttp2ServerFactory.Create(backend)`
builds it over any inner `IConnector` (the sws emulation).

- Each connection opens with the client preface (prior knowledge) or an HTTP/1.1 request
  carrying `Upgrade: h2c`, answered with `HTTP/1.1 101 Switching Protocols`, `Connection: Upgrade`,
  `Upgrade: h2c`, after which that request is stream 1. Any other opening closes the connection.
- The server's SETTINGS carry `SETTINGS_MAX_CONCURRENT_STREAMS` 100, as nghttpx's default. It
  never sends PUSH_PROMISE, so `SETTINGS_ENABLE_PUSH` 0 is honoured trivially.
- Each stream, once its END_STREAM arrives, is relayed on a new backend connection as
  `METHOD path HTTP/1.1`, `Host: <:authority>`, the other fields with nghttpx's capitalisation,
  hop-by-hop fields (`connection`, `keep-alive`, `proxy-connection`, `transfer-encoding`,
  `upgrade`, `te`, `http2-settings`) dropped, then `X-Forwarded-Proto: http` and
  `Via: 2 nghttpx` (appended to any incoming `via`). No `X-Forwarded-For`: servers.pm does not
  pass `--add-x-forwarded-for` and no case strips it. A body without `content-length` is sent
  chunked, as nghttpx does.
- The backend's reply (any `1xx` skipped; no body for `HEAD`, `204`, `304`; chunked, counted or
  read to the close) becomes `:status` and the fields lower-cased, hop-by-hop fields and
  `server` dropped, then `server: nghttpx` and `via: 1.1 nghttpx`. A refused backend is `502`.
- Flow control: every DATA byte received is given back at once with WINDOW_UPDATE on the
  connection and the stream; DATA sent never exceeds the client's connection or stream window
  (its `SETTINGS_INITIAL_WINDOW_SIZE` plus its updates) or `SETTINGS_MAX_FRAME_SIZE`.
- A client GOAWAY or close ends the session with the server's own GOAWAY.

Streams are served one after another on one task; nothing the cases do needs concurrent relays.
Out of scope for now: an HTTP/1.1 request without `Upgrade: h2c` (nghttpx would proxy it), an
upgrade request with a body, and a reply header block larger than one frame.

## Consequences

The runner can put the stand-in on `%HTTP2PORT` in one line (BL-2230); the TLS front on
`%HTTP2TLSPORT` is BL-2245.
