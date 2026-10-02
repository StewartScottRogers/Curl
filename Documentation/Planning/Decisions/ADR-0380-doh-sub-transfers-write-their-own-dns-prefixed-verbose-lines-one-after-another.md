# ADR-0380: DoH sub-transfers write their own [DNS]-prefixed -v lines, one after another

- Status: Accepted
- Date: 2026-10-02
- Task: BL-1180
- Decided by Claude under Stewart's delegation.

## Context

Under `-v --trace-config dns` (or `doh`, `network`, `all`) with `--doh-url`, curl 8.21.0 writes each
DoH sub-transfer's own `-v` lines with a `[DNS] ` prefix (measured, BL-1157 Notes): the DNS filter's
lines (doubled, `[DNS] [DNS] added`, except `[DNS] created DNS filter ...`), `Trying`, the TLS trust
and ALPN lines, `Established connection`, `using HTTP/1.x`, `upload completely sent off`, `Connection #N
... left intact` and `a DoH request is completed, K to go`, with the `>`/`<` heads and `}`/`{` data lines
unprefixed. curl runs the A and AAAA sub-transfers in parallel, so their lines interleave by poll timing.
The structured events (connection opened, TLS trust and handshake) are worded by `Curl.Output`, not by
`Curl.Networking`, which reports them.

## Decision

1. `Curl.Output`'s `DohSubTransferEvents` wraps the transfer's events: it words each structured event as
   `TransferEventInfoText` does for the platform's TLS build and reports every info line with `[DNS] `
   in front, leaving the filter's creation line with its one prefix; heads, data and TLS bytes pass on.
   The composition root gives it to `DohDnsResolver.SubTransferEvents` under the DNS trace components,
   and turns on `TracesDnsFilter` for the DoH connector, so the filter's lines come doubled as curl's do.
2. When `SubTransferEvents` is given, the queries run one after another, A's sub-transfer whole before
   AAAA's, so the lines come in a fixed order. Untraced, they still run in parallel. The poll-timing line
   `Connection #1 is not open enough, cannot reuse` is not written.
3. The DoH connections are numbered by the resolver from `#1`, after the transfer's own `#0`, as curl
   numbers them in the measured runs; the pool's count is not used, because the pool numbers the
   transfer's connection only once it has connected, after the resolve.

## Consequences

- Traced DoH resolves take two round trips in turn rather than at once; untraced runs are unchanged.
- In a run with several transfers, the DoH connections keep counting from `#1` on their own rather than
  in the run's count. That is left as it is until it is measured.
- The second sub-transfer finds the DoH server's address in the DNS cache and says so, as curl's does.

## Options considered

- Prefix inside `VerboseTransferEventWriter` and `TraceTransferEventWriter` through a shared prefix
  object, like `TraceIdsPrefix`: both writers change and the sub-transfers would still need to set it per
  event, around concurrent queries. Lost to the decorator, which changes no writer.
- Buffer each parallel sub-transfer's events and replay them in order: keeps the queries parallel but
  needs a recorder for every event type. Lost on size; the order is the same.
