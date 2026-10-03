# ADR-0388 — `--trace-config http/3` writes the QUIC connection lines and echoes the head

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1208.
Extends ADR-0375 (BL-1168), which writes the HTTP/3 stream lines.

## Context

curl 8.18.0's ngtcp2 build (curl.se's Windows build, LibreSSL 4.2.1, ngtcp2 1.21.0), run on
2026-10-02 as `curl -s -v --trace-config http/3 --http3-only https://cloudflare-quic.com/ -o NUL`,
writes these `[HTTP/3]` lines on top of ADR-0375's (I/O-loop lines left out):

```
* SSL certificate verified via OpenSSL.
* [HTTP/3] handshake complete after 42ms, remote transport[max_udp_payload=65527, initial_max_data=10485760]
* [HTTP/3] max bidi streams now 100, used 0
* [HTTP/3] peer verified
* [HTTP/3] connect -> 0, done=1
* Established connection to cloudflare-quic.com (...)
* using HTTP/3
* [HTTP/3] peer idle timeout is 180000ms, set keep-alive to 90000 ms.
* [HTTP/3] [0] OPENED stream for https://cloudflare-quic.com/
...
< HTTP/3 200 
* [HTTP/3] [0] status: HTTP/3 200 
                                   <- the bytes are "200 \r\n\n": the traced text ends in the status line's CRLF
* [HTTP/3] [0] header: date: ...
< date: ...
...
<
* [HTTP/3] [0] end_headers, status=200
...
{ [0 bytes data]
* [HTTP/3] [0] easy handle is done
* [HTTP/3] no active streams, unset keep-alive
* [HTTP/3] query conn[0]: MAX_CONCURRENT -> 99 (0 in use)
* Connection #0 to host cloudflare-quic.com:443 left intact
```

Unlike HTTP/2 (ADR-0386), each `header:` echo comes *before* its `<` line: `cb_h3_recv_header`
traces a field and then writes it. The status echo comes after, since curl writes the status
line first and traces the text it wrote.

## Decision

- `QuicDialer.WritesHttp3ConnectionLines` (set by `CurlComposition` from `TracesHttp3`) writes the
  four handshake lines between the TLS lines and `Established connection`: the time from the
  handshake's start to its completion in whole milliseconds, the server's `max_udp_payload_size`
  and `initial_max_data`, its bidirectional stream limit, `peer verified`, `connect -> 0, done=1`.
- `IMultiplexedConnection.PeerIdleTimeout` (default `null`; `QuicConnection` gives the server's
  `max_idle_timeout`, `null` for 0) lets the HTTP/3 session write the idle-timeout line as the
  connection's first request stream opens, after `using HTTP/3`. A peer with no idle timeout
  gets no line.
- `HttpResponseHeadReader.LineReporting` fires before each `<` line; over HTTP/3 it echoes
  header lines as `header: name: value`. `LineReported` echoes the status line as
  `status: ` followed by the line with its CRLF, so the console's line feed makes the empty line.
- After a successful body read, `Http3StreamConnection.ReportTransferDone` writes
  `easy handle is done`, `no active streams, unset keep-alive` when no other request stream on
  the session is in use, and `query conn[<connection number>]: MAX_CONCURRENT -> <limit - streams opened> (<in use> in use)`
  when the connection knows its stream limit.

## Consequences

- The connection lines name what curl names; the I/O-loop lines (`ingress`, `egress`, `vquic_*`,
  `cf_send`, `cf_recv`, `read_stream`) stay out, as ADR-0373 and ADR-0375 decided.
- A failed transfer writes no end lines and does not mark its stream done, so a later transfer
  on the same connection counts it as in use. Such a connection is rarely reused; curl's lines
  for that case were not measured.
- `IMultiplexedConnection` gained a defaulted member, so other implementations are unchanged.

## Alternatives considered

- **Writing the idle-timeout line in the dialer:** it would come before `using HTTP/3`, which
  the HTTP handler writes, not after it as curl does.
- **Holding the echoes like the stream lines:** the echoes must sit among the `<` lines, which
  are written as the head reader reads them, so they are written right away.
