# ADR-0182 — `--http3` and `--http3-only` parse as version options, and the console composes a `QuicDialer`

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-732.

## Context

ADR-0144 decided that HTTP/3 is hand-built and that `--http3` and `--http3-only` stop being
refused (ADR-0017) once they work. ADR-0172 (BL-731) put the connect selection in the HTTP
handler, and ADR-0180 (BL-728) gave `TcpConnector` an optional `QuicDialer`. BL-732 connects
the command line to them. What was left open: how the two options interact with the other
version options, what the TCP connection they fall back to offers through ALPN, and how the
console builds the dialer.

Measured on 2026-09-28 with curl.se's `curl 8.18.0 (x86_64-w64-mingw32) … ngtcp2/1.21.0
nghttp3/1.15.0` against `http://127.0.0.1:1/`:

| Invocation | stderr before the transfer | Result |
| --- | --- | --- |
| `--http1.1 --http3` | `Warning: Overrides previous HTTP version option` | as `--http3` |
| `--http3 --http3` | nothing | as `--http3` |
| `--http3 --http3-only` | the overrides warning | exit 3, `HTTP/3 requested for non-HTTPS URL` |
| `--http3=x` | nothing | accepted, the value ignored as for every flag |
| `--no-http3` | `curl: option --no-http3: the given option cannot be reversed with a --no- prefix` | exit 2 |

This is curl's `sethttpver` in `src/tool_getparam.c`, which every version option calls.

## Decision

1. **The options are version options.** `RequestedHttpVersion` gains `Http3` and `Http3Only`,
   set through `CommandLineOptions.SelectHttpVersion` like `--http1.1` and `--http2`: the last
   version option wins, and a different earlier one draws the overrides warning unless `-s`
   came first. `--no-http3` and `--no-http3-only` stay not reversible. The now-unused
   `CommandLineOption.UnsupportedFlag` is removed; `CommandLineRefusal.InstalledLibcurlDoesNotSupport`
   stays for ADR-0137's unimplemented options and `--tlsauthtype`.
2. **Mapping.** `HttpVersionMapping` maps them to `HttpVersionPreference.Http3` and
   `Http3Only`. The TCP handshake under either offers `h2,http/1.1` through ALPN on every
   platform: under `--http3` that is the fallback measured in ADR-0144; under `--http3-only`
   TCP is used only through a proxy (ADR-0172), which was not measured, and the same offer is
   taken as the simplest rule that keeps the two options alike.
3. **Composition.** `CurlComposition.CreateTransports` builds one `QuicDialer` per option
   group from the origin's `TlsClientOptions` on the run's clock, keeps it in
   `CurlTransports.QuicDialer`, and hands it to `CreateTcpConnector`, so `-k`, `--cacert` and
   the other TLS options judge a QUIC server as they judge a TCP one. It binds no local address
   or port: the TCP connector does not take `--interface` or `--local-port` yet either, and both
   gain them together.
4. **End points.** `EndPointRecordingConnector` forwards `ConnectMultiplexedAsync` and records
   the QUIC connection's end points; without it the interface's default would fail every QUIC
   connect with `QUIC is not available on this connector`.

The race that starts TCP when `--happy-eyeballs-timeout-ms` passes without a QUIC handshake is
BL-835; the `-v`, `%{http_version}` and `-V` output for HTTP/3 is BL-734.

## Consequences

- `--http3` and `--http3-only` run on every platform where they used to exit 2.
- The console tests run HTTP/3 end to end over `ScriptedQuicConnector`, a fake QUIC connection,
  with no socket.

## Alternatives considered

- **Keep `UnsupportedFlag` for later options.** No row uses it, and ADR-0137 already refuses
  a known option with no row with the same text. Removed.
- **Offer `http/1.1` alone on Windows under `--http3-only` through a proxy, as the default
  does.** Nothing was measured to prefer it, and it would make the two HTTP/3 options differ
  for no observed reason. Rejected.
