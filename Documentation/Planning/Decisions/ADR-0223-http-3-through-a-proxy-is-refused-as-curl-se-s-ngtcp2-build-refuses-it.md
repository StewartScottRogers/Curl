# ADR-0223 — HTTP/3 through a proxy is refused as curl.se's ngtcp2 build refuses it

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-837.
Settles what ADR-0172 section 5 left unmeasured (HTTP/3 with a proxy connected over TCP
only), under ADR-0187, which makes curl 8.21.0 the HTTP/3 reference.

## Context

Until BL-837, `--http3` and `--http3-only` with any proxy connected over TCP exactly as
without them, so `--http3-only` through a proxy succeeded over HTTP/1.1, which curl never
does. curl's `Curl_conn_may_http3` (`lib/vquic/vquic.c`) decides whether HTTP/3 may be
tried: at `curl-8_18_0` it refuses a SOCKS proxy and a tunnelling HTTP proxy; at
`curl-8_21_0` it refuses only a SOCKS proxy, and an HTTP proxy tunnels QUIC through
CONNECT-UDP with the capsule protocol (`lib/cf-setup.c`, `lib/http_proxy.c`).

### Measured 2026-09-29

curl.se's Windows build, `curl 8.18.0 (x86_64-w64-mingw32) libcurl/8.18.0 LibreSSL/4.2.1 …
nghttp2/1.68.0 ngtcp2/1.21.0 nghttp3/1.15.0 WinLDAP` (the only ngtcp2 build installed),
with `Record-CurlExchange.ps1` as the proxy on 127.0.0.1:47837 answering
`HTTP/1.1 403 Forbidden`, run as `curl -sS <options> https://example.test/`:

| Options | Exit | stderr | Proxy received |
| --- | --- | --- | --- |
| `--http3-only -x http://…` | 3 | `curl: (3) HTTP/3 is not supported over an HTTP proxy` | nothing |
| `--http3-only --proxy-insecure -x https://…` | 3 | same | nothing |
| `--http3-only -x socks5://…` (and `socks5h://`) | 3 | `curl: (3) HTTP/3 is not supported over a SOCKS proxy` | nothing |
| `--http3 -x http://…` (and `https://…`) | 56 | `curl: (56) HTTP/3 is not supported over an HTTP proxy` | `CONNECT example.test:443 HTTP/1.1` as without `--http3` |
| `--http3 -x socks5://…` (and `socks5h://`) | 97 | `curl: (97) HTTP/3 is not supported over a SOCKS proxy` | the SOCKS5 greeting `05 02 00 01` |
| `--http3-only -x http://… http://example.test/` | 3 | `curl: (3) HTTP/3 requested for non-HTTPS URL` | nothing |

With `-v`, the refusal is an info line before `Trying`; for `--http3-only` it is followed
by `closing connection #-1`. For `--http3` the exit code is the tunnel's own failure (56
for the 403, 97 for the junk SOCKS reply), but the text is the refusal: libcurl keeps the
first `failf` of a transfer in its error buffer, and the refusal came first.

## Decision

1. For an `https://` URL with `--http3` or `--http3-only` and a proxy, the HTTP handler
   reports the refusal as an info line: `HTTP/3 is not supported over a SOCKS proxy` for
   any SOCKS kind, `HTTP/3 is not supported over an HTTP proxy` for an HTTP, HTTP/1.0 or
   HTTPS proxy.
2. `--http3-only` then fails with exit 3 and that text before connecting, followed by
   `closing connection #-1`. An `http://` URL fails first with the non-HTTPS message.
3. `--http3` connects over TCP through the proxy as without it and never tries QUIC; a
   failed transfer keeps its exit code and is reported with the refusal's text; a
   successful one prints nothing more.
4. **Interim for HTTP proxies - superseded by ADR-0250 (BL-942, 2026-10-01).** An HTTP,
   HTTP/1.0 or HTTPS proxy is no longer refused: QUIC goes through it in a CONNECT-UDP
   tunnel, and decisions 1 to 3 now hold for SOCKS proxies only. The text below is kept as
   it was decided. The SOCKS rows match 8.21.0 too. The HTTP proxy rows are
   the 8.18.0 behaviour: 8.21.0 tunnels QUIC through CONNECT-UDP instead, which is new
   protocol work (RFC 9298 and RFC 9297) filed as BL-942. Until it lands, the measured
   8.18.0 behaviour is the closest measured behaviour, and it never lets `--http3-only`
   succeed over TCP.

## Consequences

- `HttpProtocolHandler.Http3ProxyRefusalOf` and `ExchangeWithoutHttp3Async` implement it;
  `HttpProtocolHandlerTests.Http3Proxy.cs` pins every row.
- A reused pooled connection does not repeat the refusal in curl (no connect filter
  runs), but the handler still reports it; the difference only shows with `-v` across
  several URLs on one command line.
- BL-942 replaces decision 4 for HTTP proxies.
