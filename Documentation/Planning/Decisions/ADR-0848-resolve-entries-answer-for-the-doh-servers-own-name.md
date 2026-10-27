# ADR-0848 — --resolve entries answer for the DoH server's own name

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3684, CGR-155).

## Context

CGR-155 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`) found that
`curl --resolve dohsrv.test:P:127.0.0.1 --doh-url https://dohsrv.test:P/dns-query http://foo.test/`
sends both DoH queries to `127.0.0.1`, while Curl looked `dohsrv.test` up with the system resolver,
failed, and exited 6. BL-3684 measured curl 8.22.0 (Schannel) with `Record-CurlExchange.ps1 -DohPort`:
the AAAA and A queries for `foo.test` both reached the DoH responder through the `--resolve` entry.
curl's `--resolve` entries go into the DNS cache, which the DoH probe handles share with the transfer
that started them, so they find the server's name there; the probes are given no `CURLOPT_RESOLVE` of
their own, so they write no `Added ... to DNS cache` lines.

ADR-0152's DoH connector was a `TcpConnector` of its own over the system resolver, with no
`--resolve`, `--connect-to`, `-x` or `-4`/`-6`.

## Decision

`EngineComposition.CreateDohConnector` takes the option group's `ResolveOverrides` and hands them to
its `TcpConnector`, then loads them at once with `LoadResolveEntries(NoTransferEvents.Instance)`, so
the entries answer for the DoH server's host and port and no DoH sub-transfer writes their lines.
`CreateDnsResolver` and the conformance harness's `DialingResolverOf` pass
`ResolveOverrides.Parse(network.Resolve)`. `--connect-to`, `-x` and `-4`/`-6` still do not apply to the
DoH connections.

An entry that does not parse fails the transfer's own connect with exit 49 before the DoH resolver is
asked, so the DoH connector never meets it.

## Consequences

- The repro of CGR-155 sends both DoH queries through the entry, as curl does.
- Sharing the run's `DnsCache` itself was rejected: the DoH resolver is built before the transfer's
  connector, which loads the entries into that cache only when a transfer starts, and the cache is
  `null` outside the curl tool; loading the entries into the DoH connector's own cache is the smallest
  change that matches curl.
- Tests: `Curl.Engine.UnitTests/Cgr155Tests.cs`.
