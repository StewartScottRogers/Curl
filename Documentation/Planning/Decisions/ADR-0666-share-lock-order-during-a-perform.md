# ADR-0666: The share lock callbacks during a perform follow libcurl's order

- Status: Accepted
- Date: 2026-10-17
- Decided by Claude under Stewart's delegation
- Task: BL-2603 (amends ADR-0606)

## Context

Upstream test506 prints every share lock and unlock call. libcurl 8.22.0 takes them during a perform in this
order: the cookie lock once for loading `CURLOPT_COOKIEFILE`'s files (`Curl_cookie_loadfiles`, only when files are
set) and once more before resolving; the DNS lock to prune the cache, to look the host up and, on a miss, to add
it (three pairs the first time, two once cached); the cookie lock for the request's `Cookie` header and for each
`Set-Cookie`; and one DNS pair once the transfer is done, as the connection's DNS entry is unlinked. Curl took the
DNS lock once and then the cookie lock once as the perform attached.

## Decision

`ManagedShareHandle.Attach` takes the shared caches in that order: the public suffix list, connections and TLS
sessions as before, then the cookie lock (once more when the handle's `CookieFiles` are set), then the DNS lock
(prune, look-up, and add the first time the share sees the `host:port`, which it remembers in a set of the names
its attached performs resolved), then HSTS. `LockDnsAtDone` takes one DNS pair when the perform ends. The HTTP
handler's cookie calls already go through `LockedCookieStore`, one pair per call. The share models libcurl's lock
calls; the real look-ups still go through the shared `DnsCache`, whose entries are internal to
`Curl.Networking`, so the share keeps its own record of the names seen rather than asking the cache.

## Consequences

test506's `<stdout>` matches through every perform (line 131). The `CURLOPT_COOKIELIST RELOAD` and
`CURLINFO_COOKIELIST` lines of a handle that never performs are left to BL-2605.
