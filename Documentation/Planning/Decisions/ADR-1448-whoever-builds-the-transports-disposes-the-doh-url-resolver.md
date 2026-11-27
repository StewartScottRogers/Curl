# ADR-1448 — Whoever builds the transports disposes the `--doh-url` resolver

- **Status:** Accepted
- **Date:** 2026-11-26
- **Amends:** ADR-1441, whose Consequences left the resolver undisposed

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6057.

## Context

ADR-1441 made `DohDnsResolver` keep its connections until `DisposeAsync`, and nothing called it. A
long-lived `Curl.Api`, `Libcurl` or engine host builds new transports, and so a new resolver, on
every perform, so each one left its connections open.

## Decision

- `CurlTransports` is `IAsyncDisposable`: it closes what its `DohDnsResolver` kept. It leaves any
  other resolver alone, since an `EngineDialing`'s resolver is its caller's, and it leaves the pooled
  connections to whoever holds the `PoolingConnector`.
- In the console, an option group's `TransferDispatch` disposes its connection pool and then its
  `KeptDohConnections`: the group's transports, or the DoH resolver the dialing runner built.
- In the managed engine, each perform keeps the transports it builds and disposes them when it
  ends, however it ends. The resolver is not carried to the handle's next perform, because that
  perform builds its own.

## Consequences

A library handle reuses no DoH connection from one perform to the next, where libcurl keeps it in the
handle's connection cache. Reusing it needs a resolver that outlives one perform's transports.
