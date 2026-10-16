# ADR-0568 — `Libcurl`'s network option setters check values as curl 8.22.0's `setopt.c` does, and `CURLOPT_PROTOCOLS` bitmasks become `PROTOCOLS_STR` names

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2402.

## Context

BL-2402 adds a setter row (ADR-0560) for every `CURLOPT_*` that `EasyNetworkOptions` holds. The
record's shape is fixed by `Curl.Api.UnitLibrary`: it carries the protocol lists only as
`CURLOPT_PROTOCOLS_STR` strings, times as `TimeSpan`, and `CURLOPT_CURLU` as a URL string, written
before `ICurlUrlHandle` existed.

## Decision

1. The rows live in `Libcurl/Options/Network`. `EasyOptionSetters.AddNetworkSetters` calls
   `EasyNetworkOptionSetters.AddTo`, one named static method per row, as ADR-0565 does for HTTP.
2. Range checks follow `setopt.c`, answering `BadFunctionArgument`: `PORT`, `LOCALPORT` and
   `LOCALPORTRANGE` outside 0..65535; `ADDRESS_SCOPE` outside 0..`UINT_MAX`; `MAXCONNECTS` below 1;
   `IPRESOLVE`, `CONNECT_ONLY` and `USE_SSL` outside their defined values; a negative time or
   `TCP_KEEPCNT`; `DNS_CACHE_TIMEOUT` below -1, where -1 is forever (`Timeout.InfiniteTimeSpan`).
   Every count and time is cut to `INT_MAX` of its unit, the largest a 32-bit Windows `long`
   carries, so no `TimeSpan` overflows.
3. `PROTOCOLS_STR` and `REDIR_PROTOCOLS_STR` are stored as given once `protocol2num` would accept
   them: `all`, or a comma-separated list naming at least one known scheme in any letter case (unknown
   names are skipped). `null`, or a list naming none, is `BadFunctionArgument`. The deprecated
   `PROTOCOLS` and `REDIR_PROTOCOLS` bitmasks are turned into the same strings: `all` when every
   `CURLPROTO_*` bit is set (`CURLPROTO_ALL`), else the set bits' names, with `ws` and `wss` as bits
   30 and 31 as in `urldata.h`.
4. `DNS_USE_GLOBAL_CACHE` is deprecated and ignored with `Ok`, as upstream does. `DNS_SERVERS`,
   `DNS_INTERFACE`, `DNS_LOCAL_IP4/6` and `INTERFACE` are stored without parsing; the engine reads
   them, as the record's doc comment says. `RESOLVE` and `CONNECT_TO` store a copy of the list, and
   `null` empties it.
5. `CURLOPT_CURLU` is left to BL-2462: it needs `CurlUrlHandle` (BL-2438) and an engine-handle field
   on the record, so it still answers `NotBuiltIn`.

## Consequences

- An unknown `PROTOCOLS_STR` name is kept in the stored string; the engine must skip it as
  `protocol2num` does.
