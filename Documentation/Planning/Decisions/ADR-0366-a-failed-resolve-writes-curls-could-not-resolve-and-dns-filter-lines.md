# ADR-0366 — A failed resolve writes curl's `Could not resolve` and `[DNS]` filter lines

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1157, following ADR-0356 decision 4. Measured on 2026-10-02 with `Record-CurlExchange.ps1`
against curl 8.21.0 (Schannel), full stderr in BL-1157 Notes:

- `-s -v http://nonexistent.invalid:P/` writes `Could not resolve host: nonexistent.invalid` twice,
  `Could not resolve: nonexistent.invalid:P` and `closing connection #0`. Curl wrote only the last.
- Under `--doh-url` the first line comes once.
- Under `--trace-config dns` the filter adds `[DNS] cache negative name resolve for H:P type=A+AAAA`
  before `Could not resolve:`, then `[DNS] error resolving: 6`, `Curl_conn_connect(block=0) -> 6, done=0`,
  `Curl_conn_connect(), filter returned 6`, `[DNS] [1] shutdown async`, and after
  `closing connection #0` `[DNS] [1] destroy async`. Under `-4` the filter is created with
  `queries=1` and the type is `A`; under `-6`, `queries=2` and `AAAA`.
- The threaded resolver also writes `queueing query`, `resolve incomplete` and `Host H:P resolved IPv4: (none)`
  lines whose number follows its poll timing.
- The DoH sub-transfers' own `[DNS]`-prefixed lines need the output writers to prefix structured
  events (`Curl.Output`), and the proxy, Unix socket and negative-cache-entry variants need their own
  measurements.

## Decision

1. `TcpConnector` writes `Could not resolve host:` and `Could not resolve: H:P` on the target's events
   for a direct connect whose resolver answered nothing (not for a negative cache entry, not for exit 43).
   The first line comes twice for every resolver but `DohDnsResolver`: the system resolver measured so,
   and the `--dns-servers` client, which no Windows curl build has, is taken to behave as the system one.
2. `DnsFilterTraceEvents` brackets `Could not resolve:` with the measured trace lines, and
   `DnsFilterTraceEvents.Start` takes the `-4`/`-6` family for `queries=` and `type=`.
3. `AsyncResolveTeardownTraceEvents` wraps each transfer's events under `--trace-config dns`, `doh`,
   `network` or `all` (`CurlCommandRunner.SetUpTransferEvents`), and writes `[DNS] [1] destroy async`
   after the `closing connection #N` that follows `[DNS] [1] shutdown async`: the protocol writes that
   line after the connect has returned, out of the connect's reach.
4. The threaded resolver's poll-timed lines are not written, as ADR-0356 decided for `done=0`.
5. Split out as tasks: BL-1180 (the DoH sub-transfers' lines) and BL-1181 (proxy, Unix socket,
   negative cache entry, and the `resolve complete` and async lines after a name that resolved).

## Alternatives considered

- Writing the failed-resolve lines only under `--trace-config`: plain `-v` measured them too.
- Writing `[DNS] [1] destroy async` from the protocol handlers: every handler would learn about a trace
  component; one decorator in the run does it for all.

## Consequences

`curl -v` on a name that does not resolve prints curl's lines, and `--trace-config dns` adds the filter's.
Tests: `DnsFilterTraceEventsTests`, `AsyncResolveTeardownTraceEventsTests`, `TcpConnectorTests.DnsFilterTrace`,
`CurlCommandRunnerFailedResolveTraceTests`.
