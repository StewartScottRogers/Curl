# ADR-0053 — Curl.Console refuses a proxy tunnel the connector cannot open yet, with exit 4

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"; recorded
  by BL-238, 2026-09-27).

## Context

BL-238 wires proxy selection into `Curl.Console`: `TransferProxySelection` asks
`ProxySelector` (ADR-0024) for the proxy from `-x` or a `--socks` option, `--noproxy` and the
proxy environment variables, lets `-U` replace its credential, and puts it in
`HttpRequestOptions.ForwardProxy` with `-p` as `ProxyTunnel`. `HttpProtocolHandler` forwards a
plain `http` request to an HTTP or HTTPS proxy itself (BL-183) and hands every other route to
the connector as `ConnectTarget.Proxy`.

`TcpConnector` tunnels only through `Http` and `Http10` proxies (ADR-0023). For a SOCKS or
HTTPS proxy it throws `NotSupportedException`; SOCKS is BL-213 and HTTPS proxies BL-266, both
in Backlog. Wiring the selector without a guard would let `curl --socks5 h http://x/` or
`curl -x https://h https://x/` end in an unhandled exception and a stack trace.

## Decision

`TransferProxySelection` ends an `http` or `https` transfer whose route needs a tunnel the
connector cannot open - through any SOCKS proxy, or through an HTTPS proxy for an `https` URL,
under `-p`, or under `-L` (a redirect hop to `https` keeps the first URL's proxy, BL-329) - with exit 4 (`CURLE_NOT_BUILT_IN`) and
`Unsupported proxy '<host>:<port>', Curl cannot tunnel through a <kind> proxy yet`, before
anything is connected. Since ADR-0056 (rule 6, BL-338) every other networked scheme is refused the
same way through any SOCKS or HTTPS proxy, because its handler hands `ITransferContext.Proxy` to
the connector's tunnel; `file` never uses a proxy and is never refused.

Exit 4 is the code libcurl itself uses when a proxy kind is missing from the build
(`Unsupported proxy '...', libcurl is built without the HTTPS-proxy support.` in `url.c`), so a
script that checks exit codes sees what a curl built without the feature gives. The message is
Curl's own and says "yet": it is not measured curl text and no test pins it as such.
BL-328 removes the guard once BL-213 and BL-266 land.

## Consequences

- No proxy option makes the executable crash; the unsupported tunnels fail cleanly.
- Until BL-328, `--socks*`, `socks*://` and tunnelled HTTPS proxies differ from curl 8.21.0,
  which supports them.
- The guard covers `dict`, `gopher`, `telnet` and every other non-`file` scheme too (ADR-0056),
  so no scheme reaches `TcpConnector` with a proxy it cannot tunnel through.

## Alternatives considered

- **No guard, let the connector throw.** Lost: an unhandled exception is not a drop-in
  replacement's behaviour for any input.
- **Ignore the proxy and connect directly.** Lost: silently bypassing a proxy the user named
  can leak traffic the user meant to route elsewhere.
- **Catch `NotSupportedException` in the runner.** Lost: it would hide any other
  `NotSupportedException` too, and the route is known before connecting.
