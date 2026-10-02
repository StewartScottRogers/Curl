# ADR-0356 — `--trace-config dns` writes the DNS filter's and the DoH resolver's lines around the connect

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1102, following ADR-0318: under `-v --trace-config dns`, `doh` or `all`, curl 8.21.0 (Schannel)
writes `[DNS]` lines for its DNS connection filter, and under `--doh-url` the DoH resolver's lines.
Measured on 2026-10-02 with `Record-CurlExchange.ps1` (BL-1102 Notes):

- A plain transfer to `127.0.0.1:P` writes three filter lines before `Trying`, one
  `Curl_conn_connect(block=0) -> 0, done=0` after each `Trying`, two lines before
  `Established connection` and two after it. A refused dial ends with `-> 7, done=0` and
  `filter returned 7` after `Failed to connect to`, with extra `done=0` lines whose count depends on
  how often curl's multi loop polled.
- `--trace-config dns` and `--trace-config doh` write byte-identical output with `--doh-url`: either
  turns on both the filter's lines and the DoH lines. Without `-v` nothing is written.
- Under `--doh-url` curl also writes `resolve incomplete` and `Curl_conn_connect` lines on every poll
  of its multi loop, and each DoH sub-transfer's own `-v` lines prefixed `[DNS] `.

`DohDnsResolver` (BL-850) already formats the DoH lines, but the composition builds it once per run,
before any transfer exists, and `IDnsResolver.ResolveAsync` carries no events.

## Decision

1. `dns`, `doh` and `all` are one switch (`CurlComposition.TracesDns`), as measured.
2. `TcpConnector.TracesDnsFilter` wraps a direct connect's events in `DnsFilterTraceEvents`, which
   writes the measured lines around the connector's own `Trying`, `Established connection` and
   `Failed to connect to` reports. Only one `done=0` line follows each `Trying`: the extra ones are an
   artefact of curl's poll timing, not of the transfer, and no two curl runs agree on them.
3. The DoH lines reach the resolving transfer through `FlowScopedTransferEvents`, an `AsyncLocal`
   view: the resolver reports to it, and `TcpConnector` points it at the target's events before each
   look-up (`ResolverEvents`). An `AsyncLocal` keeps serial and `-Z` transfers apart without widening
   `IDnsResolver`, a contract every protocol shares.
4. Left for follow-up work (filed as a task): the multi loop's `resolve incomplete` lines, the DoH
   sub-transfers' `[DNS]`-prefixed `-v` lines, the lines of a failed resolve
   (`cache negative name resolve`, `error resolving: 6`), and connects through a proxy or a Unix socket.

## Alternatives considered

- An events parameter on `IDnsResolver.ResolveAsync`: changes the shared abstractions contract and
  every resolver for one trace option.
- Building the DoH resolver per transfer: loses the run's one resolver that `--ech` and both
  connectors share.
- Writing every polling line: the count is not deterministic in curl, so no test could pin it.

## Consequences

`curl -v --trace-config dns` prints curl's filter lines for every direct TCP connect, and with
`--doh-url` the DoH answer's lines reach the transfer's `-v` and trace output, under `-Z` too.
