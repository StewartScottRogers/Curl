# ADR-0927: A CONNECT reply fails "Invalid response header" only for a later line without a colon

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3610)
- Relates to: [ADR-0023](ADR-0023-the-connector-tunnels-through-an-http-proxy-as-curl-8-21-0-does.md)

## Context

CGR-081: BL-1857 made `HttpProxyTunnel` fail a CONNECT reply with exit 43, `Invalid response header`,
as soon as its first line ended without starting with `HTTP/`, generalising from upstream test750.
curl 8.22.0 (Schannel, Windows; the report found 8.18.0 OpenSSL on Linux the same) does not. Measured
with `Record-CurlExchange.ps1`, `curl -sS -p -x http://127.0.0.1:P http://example.test/`:

- `garbage\r\n\r\n`, `ICY 200 OK\r\n\r\n`, `garbage\n\n`, `<html>\r\n\r\n`, `garbage\r\nFoo: bar\r\n\r\n`
  and `garbage\r\n Foo\r\n\r\n`: exit 7, `CONNECT tunnel failed, response 0`.
- `garbage\r\nFoo\r\n\r\n`, `garbage\r\nFoo\r\n` (then close), `HTTP/1.1 200 OK\r\nFoo\r\n\r\n` and
  test750's `<html>\n<h1>400 Bad request</h1>\n</html>\n`: exit 43, `Invalid response header`.
- `garbage\r\n` then close: exit 56, `Proxy CONNECT aborted`.

So the first line is never refused: one that is not `HTTP/1.0` or `HTTP/1.1` only leaves the status 0
(CGR-080). The 43 comes from curl's header list refusing a later line that holds no colon and is not a
folded continuation (starting with a space or tab), as soon as that line ends.

## Decision

`HttpProxyTunnel.ReplyAfterLineEnd` fails the reply with exit 43 when a line after the first ends, does
not start with a space or tab, and holds no colon; it no longer checks the first line for `HTTP/`.

## Consequences

- A garbage status line reaches `ParseReply` with status 0 and fails the tunnel with exit 7, as curl does.
- A `2xx` reply with a colon-less field is now exit 43 instead of an open tunnel, as curl does.
- `Cgr081Tests` in `Curl.Networking.UnitTests` pins each measured case.
