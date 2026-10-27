# ADR-0851 — Each library easy and multi handle resolves names in a DNS cache of its own

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3680, CGR-151). Amends ADR-0789.

## Context

ADR-0789 gave every library easy handle and multi handle its own connection pool, TLS sessions,
cookies and HSTS, but kept the engine's DNS cache shared between them, because upstream test1512
gives only the first of two easy handles a `CURLOPT_RESOLVE` entry and expects the second to reach
the same host. The third-party gap report's CGR-151 measured real libcurl 8.22.0 (Schannel on
Windows, OpenSSL on Linux): a new easy handle with no share handle that performs a URL another
handle already resolved still calls its `CURLOPT_RESOLVER_START_FUNCTION`, and a callback returning
1 ends the perform with `CURLE_ABORTED_BY_CALLBACK` (42) before any request. Curl found the name in
the shared cache, never called the callback, and sent the request.

libcurl 8.22.0's source says the same: `dnscache_get` in `lib/vdns/dnscache.c` answers the share
handle's cache when DNS is shared, else the cache of the handle's multi, and `curl_easy_perform`
gives each easy handle a multi of its own (`data->multi_easy`, `lib/easy.c`).
`CURLOPT_DNS_USE_GLOBAL_CACHE` is ignored (`lib/setopt.c`).

## Decision

- `ManagedEngineSharedState.WithOwnCaches()` gives each state a new, empty `DnsCache`, so each
  library easy handle and each multi handle looks names up in its own; a share handle sharing DNS
  still links them (ADR-0606), and the curl tool's transfers keep the engine-wide cache (ADR-0542).
- Upstream test1512 comes off `PassingUpstreamCases.txt`: in the harness its second handle no longer
  finds the first handle's `CURLOPT_RESOLVE` entry. The measured libcurl behaviour and libcurl's own
  source win over a case whose pass in upstream's suite this lane could not explain without
  building `lib1512` against real libcurl; BL-3913 checks that.

## Consequences

- A new handle's resolver-start callback runs for every name it has not looked up itself, and
  `CURLMOPT_NETWORK_CHANGED`'s `ClearDns` empties only that multi handle's names.
- test1512 shows `Inconclusive` until BL-3913 says how upstream's run of it reaches the second host.
