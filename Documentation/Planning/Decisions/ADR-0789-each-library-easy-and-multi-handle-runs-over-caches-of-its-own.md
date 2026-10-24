# ADR-0789: Each library easy handle and each multi handle runs over caches of its own

- **Status:** Accepted; amends ADR-0542, ADR-0594 and ADR-0606
- **Date:** 2026-10-23

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3271
(split from BL-3204, report 29: handles of one library leak cookies and connections).

## Context

`ManagedCurlEngine` kept one connection pool, TLS session cache, DNS cache, cookie store and HSTS
cache, and every easy handle it made ran over them. That is right for the curl tool, whose
transfers share them across one run (ADR-0542), but not for libcurl: upstream gives each easy handle
(`curl_easy_init`, `curl_easy_duphandle`) its own cookies, HSTS cache and, outside a multi handle,
its own connection pool, DNS cache and TLS sessions; a multi handle pools connections, TLS sessions
and resolved names for the handles in it; a share handle links only the kinds named with
`CURLSHOPT_SHARE`. So a cookie one handle received reached another unrelated handle, and the second
reused the first one's connection.

## Decision

- `ManagedEngineSharedState.WithOwnCaches()` makes a state over the engine's clock, numbering, multi
  owners, performer and DNS cache with an empty connection pool, TLS session cache, cookie store and HSTS cache of
  its own. The resolved names stay shared: upstream test1512 gives only the first of two easy handles a
  `CURLOPT_RESOLVE` entry and libcurl 8.22.0 still reaches the second's host through it, so libcurl's easy handles
  see each other's resolved names (`CURLOPT_DNS_USE_GLOBAL_CACHE` itself stays ignored, as upstream ignores it). `CreateEasy`, `Duplicate` and `CreateMulti` each
  take one; the engine's own state stays as `EngineWide`, and `MultiCaches` keeps each multi handle's.
- A perform runs over, in order: its share handle's caches (ADR-0606); the engine-wide caches when
  `ToolSettings` is set (the curl tool, ADR-0542); while the handle is in a multi handle, the multi's
  connections, TLS sessions and resolved names with the handle's own cookies and HSTS (`InMulti`);
  otherwise the handle's own.
- `Duplicate` (`curl_easy_duphandle`) gives the copy caches of its own whose HSTS cache starts with the source
  handle's entries (`DuplicatedWithOwnCaches`), as upstream's duphandle copies the HSTS cache (upstream test1922).
- The engine keeps every pool `WithOwnCaches` makes (`OwnConnectionCaches`, weakly) and its `DisposeAsync` closes
  them before the engine-wide pool, so an FTP control connection left in a handle's or a multi handle's pool still
  sends `QUIT` at `curl_global_cleanup` (upstream tests 525 to 593, 753, 1501).
- Cleanup closes the idle connections the handle owns in the pool its transfers used, so a handle in a
  multi handle or under `ToolSettings` leaves the pooled connection for the next handle there.
- `CURLSHOPT_UNSHARE` keeps upstream's `CURLSHE_IN_USE` while a handle is attached; once unshared,
  each handle's cookies are its own again.

## Consequences

Two unshared handles send no cookie the other received and each opens its own connection
(`CURLINFO_NUM_CONNECTS` 1); a cookie-only share crosses cookies but not connections, a
connection-only share reuses the connection but crosses no cookie, a DNS-only share crosses neither.
`CURLMOPT_MAXCONNECTS` (ADR-0605) now caps the multi handle's own pool. The curl tool's behaviour is
unchanged.
