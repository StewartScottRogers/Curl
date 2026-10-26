# ADR-0819: An HTTP/2 stream reset with HTTP_1_1_REQUIRED is sent again over HTTP/1.1

- **Status:** Accepted
- **Date:** 2026-10-26
- **Decided by Claude under Stewart's delegation** (BL-3561, third-party gap report CGR-032)

## Context

A server that needs a client certificate or TLS renegotiation, IIS among them, answers an
HTTP/2 request with RST_STREAM HTTP_1_1_REQUIRED (0xd). Curl failed the transfer with exit 92,
`HTTP/2 stream 1 was not closed cleanly: HTTP_1_1_REQUIRED (err 13)`, and made no second
connection. curl 8.18.0 with nghttp2 (the Windows reference build has no HTTP/2) reports that
line on `-v` only, then `Downgrades to HTTP/1.1`, and sends the request again, its body too,
as `HTTP/1.1` on a new connection, whose TLS handshake offers `http/1.1` alone; it exits 0
and `-w '%{http_version} %{num_connects}'` gives `1.1 2` (measured with
`Record-CurlExchange.ps1 -Http2Origin ... -Http2ResetCode 13`, BL-3561 Notes). It is
`multi_runsingle`'s `Curl_h2_http_1_1_error` branch, which sets the transfer's allowed HTTP
versions to HTTP/1.x.

## Decision

`Http2StreamConnection` marks the exit 92 failure for a reset with HTTP_1_1_REQUIRED
`HttpTransferException.RequiresHttp11`. `HttpProtocolHandler.FailedOutcome` answers it with
`Http11RequiredOutcome`: it writes `Downgrades to HTTP/1.1` to `-v`, closes the HTTP/2
connection, and resends the request once through `HttpRequestPlan.AfterHttp11Required`, whose
options ask for HTTP/1.1 and whose connect target offers only `http/1.1` through ALPN
(`DowngradedToHttp11`, kept by every later plan, so a redirect after it stays on HTTP/1.1 as
curl's does). A body is rewound as for a refused stream. A plan already downgraded is never
downgraded again, so a server that keeps resetting cannot loop.

## Consequences

The three CGR-032 repros give curl's exit code, stdout and request bytes on both connections.
curl leaves the HTTP/2 connection open until it exits and only then sends its GOAWAY; Curl
closes it, GOAWAY included, before it connects again. The bytes on each connection are the
same; only their order across the two connections differs, which no server can tell from a
single connection.
