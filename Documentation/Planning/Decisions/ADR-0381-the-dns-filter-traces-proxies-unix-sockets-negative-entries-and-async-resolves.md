# ADR-0381: The DNS filter's trace covers proxies, Unix sockets, negative cache entries and asynchronous resolves

- Status: Accepted
- Date: 2026-10-02
- Task: BL-1181
- Decided by Claude under Stewart's delegation.

## Context

ADR-0366 (BL-1157) gave `-v --trace-config dns` curl 8.21.0's `[DNS]` filter lines for a direct
connect and for a name the resolver answered nothing for. Measured on 2026-10-02 against curl 8.21.0
(mingw, Schannel), four more routes write their own (BL-1181 Notes):

- Through a proxy, plain (`-x`) or tunnelling (`-p -x`), the filter is created for the proxy's host
  and port, and a refused dial ends with the same exit 7 lines.
- Over a Unix socket the filter is `created DNS filter for <path>:0, transport=6, queries=3` and
  `cf_dns_start unix-domain-socket <path>:0`, and a refused (immediate) connect writes no
  `Curl_conn_connect(block=0) -> 0, done=0` after `Trying`.
- A negative DNS cache entry (`-6` with an IPv4-only `--resolve`) writes
  `[DNS] cache entry does not have type=AAAA addresses` before `Negative DNS entry`; plain `-v` then
  writes `Could not resolve host: foo` once, `Could not resolve: foo:P` and `Could not resolve: foo`;
  the trace ends with the exit 6 lines and no asynchronous resolve lines.
- A name the resolver looked up (the system's threaded resolver or DoH) writes
  `[DNS] resolve complete for H:P` before `Host H:P was resolved.`, and after a refused dial
  `[DNS] [1] shutdown async`, then `[DNS] [1] destroy async` after `closing connection #0`.
  `localhost`, which curl answers itself, and a DNS cache answer write neither.

## Decision

1. `DnsFilterTraceEvents` keeps the host and port it was started for and recognises the connector's
   own `-v` lines: `Negative DNS entry`, `Hostname H was found in DNS cache` and
   `Host H:P was resolved.`. From them it writes the cache entry's type line, the resolve's
   completion (not for a cached name or `localhost`) and the shutdown after a refused dial's exit 7.
   `AsyncResolveTeardownTraceEvents` already writes the destroy line after the shutdown line.
2. `TcpConnector` writes the negative entry's three `Could not resolve` lines under plain `-v` too,
   as curl does. The last, naming the host alone, is where the filter's exit 6 lines go.
3. A tunnelling proxy's connect wraps its events in the DNS filter for its first hop (the proxy, or
   the pre-proxy) only. The `[SETUP]`, `[HAPPY-EYEBALLS]` and `[TCP]` filters stay on the direct path,
   because their lines through a proxy were not measured. A plain forward proxy already goes through
   the direct path and so writes all of them.
4. A Unix socket's connect wraps its events through `DnsFilterTraceEvents.StartOverUnixSocket`, with
   the path as `--unix-socket` gave it. A connect that succeeds writes the same connected-chain lines
   as a host's. These lines were not measured, because `Record-CurlExchange.ps1` serves no Unix
   socket. A path too long for `sun_path` fails before the filter and writes none of its lines.
5. The shutdown line is written only after a refused dial. No shutdown is written for a looked-up name
   whose connect succeeds, because that was not measured either.

## Consequences

Each of the four routes prints curl's measured lines, pinned in `Curl.Networking.UnitTests`
(`TcpConnectorTests.DnsFilterTraceRoutes`, `DnsFilterTraceEventsTests`) and
`Curl.Console.UnitTests` (`CurlCommandRunnerDnsFilterRouteTraceTests`). The filter relies on the
exact wording of the connector's own `-v` lines. That wording is pinned by those same tests, so a
change to it fails them. The threaded resolver's poll-timed `queueing query` and
`resolved IPv4/IPv6` lines are still left out (ADR-0366).
