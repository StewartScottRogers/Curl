# ADR-0208 — Alt-svc seams: a route on the connect target, a store on the HTTP options

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-878.
Builds on ADR-0175 (`AltSvcCache`) and ADR-0079 (`--connect-to`); BL-623 wires the two together.

## Context

`--alt-svc` needs three things below `Curl.Console`: dialling an alternative in place of the
origin, the `Alt-Used` request header, and handing each `Alt-Svc` response header to the cache
while the head is read, so the `* Added alt-svc` lines come before the `< Alt-Svc:` line. The
measurements are in BL-623's Notes (curl 8.21.0 mingw Schannel); the `Alt-Used` slot was measured
in BL-878 with `Record-CurlExchange.ps1 -Tls` and `-k --alt-svc cache.txt -e http://r/ -b a=b -H "X-A: 1"`:
`Referer`, then `Alt-Used: localhost:18443`, then `Cookie`, then the `-H` values. With
`-H "Alt-Used: mine"` curl sends only the custom one, in the `-H` place.

## Decision

1. `Curl.Protocol.Abstractions` gains `AltSvcAlternative` (ALPN ID, host, port), `AltSvcRoute`
   (the origin's ALPN ID and the alternative) and `IAltSvcStore`, whose one member
   `StoreFromResponse(origin, header, now)` returns the alternatives it added. The store, like
   `ICookieStore`, reads no clock; unlike it, the handler (not the store) writes the `-v` lines,
   so the store stays free of `ITransferEvents`.
2. `HttpRequestOptions.AltSvcRoute` and `HttpRequestOptions.AltSvcStore` carry both per transfer.
   The handler copies the route to `ConnectTarget.AltSvcRoute` (not for a forward proxy, whose
   target is the proxy) and sends `Alt-Used: <host>:<port>` after `Proxy-Connection` and before
   the h2c `Upgrade` and `Cookie`, left out when an `-H` value names it. It hands each `Alt-Svc`
   header of an `https` response to the store and reports `Added alt-svc: <host>:<port> over <id>`
   per alternative returned; over `http` the store is never called.
3. `TcpConnector` applies the route only when no `--connect-to` mapping matched (and none failed
   to parse), reporting `Alt-svc connecting from [<id>]<host>:<port> to [<id>]<host>:<port>` first,
   and dials it as a mapped destination, so exit 7 names it after `via` and TLS verifies the
   origin. The QUIC path shares the same step.
4. `ConnectionPoolKey` includes the alternative's host and port, so a connection opened to an
   alternative never serves a request meant for the origin directly, nor the reverse.
5. Whoever sets a route (BL-623's console wiring) leaves it null when a `--connect-to` mapping
   matches, so `Alt-Used` is never sent for a connection that went elsewhere.

## Consequences

BL-623 only builds `AltSvcCache`, looks up the route and sets the two options. The
`Connection #0 to host <alternative> left intact` line curl prints names the alternative; which
host that line names is the console's concern and is left to BL-623.
