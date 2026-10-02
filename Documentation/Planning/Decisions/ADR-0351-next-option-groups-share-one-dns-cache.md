# ADR-0351 — The `--next` option groups share one DNS cache

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1053.
Amends ADR-0113, which kept the DNS cache in each `TcpConnector` for its life; follows ADR-0285,
which shares the connection cache the same way.

## Context

Each `-:`/`--next` option group builds its own transports (`CurlComposition.CreateTransports`),
so each group's `TcpConnector` held a DNS cache of its own, and a later group looked a host up
again. curl 8.21.0 (mingw, Schannel) keeps one DNS cache for the whole command line. Measured on
2026-10-02 with `Record-CurlExchange.ps1`, `-sv` in both groups and `Connection: close` responses
so each group opens its own connection (BL-1053 Notes):

- A host group 1 resolved (`localhost`) or dialled (`127.0.0.1`) is `Hostname H was found in DNS
  cache` in group 2.
- Group 1's `--resolve foo.example:P:127.0.0.1` entry, and its `+` (non-permanent) form, answer
  group 2, which has no `--resolve`; the entry's `Added` line is printed once, by group 1.
- Group 2's own `--resolve localhost:P:127.0.0.1` over a host group 1 resolved prints
  `RESOLVE localhost:P - old addresses discarded` before its `Added` line, and answers with the
  entry's address only.

## Decision

- `Curl.Networking` gets a public `DnsCache`, the cache `TcpConnector` used to keep in a private
  dictionary: entries keyed on `host:port` without regard to letter case. `TcpConnector` takes an
  optional `dnsCache` (its own when none is given) and exposes it as `TcpConnector.DnsCache`.
- `CurlComposition.CreateRunner` makes one `DnsCache` per run and hands it, through
  `CreateTransports` and `CreateTcpConnector` (`runDnsCache`), to every option group's connector.
- Each group still loads its own `--resolve` entries at each transfer's start
  (`LoadResolveEntries`), now into the shared cache, so a later group's entry replaces an earlier
  answer with curl's `old addresses discarded` line; nothing else about the cache changed.

## Consequences

- A later group's `-v` lines match curl's for a host an earlier group resolved.
- The cache is keyed on host and port only, as before. Groups that differ in `-4`/`-6` share an
  answer cached under one family; `AnswerFromCache` still filters it to the asking group's family.
  This was not measured and is left as ADR-0143 had it within one group.
- `--dns-servers` or `--doh-url` in one group and not another still share the cache; curl's cache
  is the share handle's and is not keyed on the resolver either.

## Alternatives considered

- **Share the cache by sharing one `TcpConnector`.** Lost: the connector also carries each group's
  TLS, proxy, binding and timeout settings, which differ per group.
- **Keep a cache per group.** Lost: it prints a fresh lookup where curl prints `found in DNS cache`,
  and looks the host up again.
