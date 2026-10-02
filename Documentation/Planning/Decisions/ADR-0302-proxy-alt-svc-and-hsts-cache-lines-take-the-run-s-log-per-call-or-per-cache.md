# ADR-0302 — Proxy, alt-svc and HSTS cache lines take the run's log per call or per cache

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1072.

## Context

BL-921 wrote retries, redirects, the watchdogs and HSTS upgrades to Curl's own diagnostic log
(ADR-0222) but left out which proxy a transfer uses, the `--alt-svc` alternatives stored and
used, and the HSTS entries stored and expired. `CurlComposition.CreateTransferDispatch` builds
each option group's `ProxySelector` before the run's log exists: `CurlCommandRunner` opens the
log once the command line is accepted (ADR-0236). Real curl has no such log, so there is no
output to match; only the never-logged rules of ADR-0222 bind.

## Decision

1. **The proxy log is passed per call.** `ProxySelector.TrySelect` takes an optional
   `IDiagnosticLog` as its last parameter, and `Curl.Console`'s `TransferProxySelection.TrySelect`
   passes the run's log through, which the runner holds at every call (the first URL's and each
   redirect hop's). The selector stays immutable and shared by the group; no setter is added that
   a run would have to remember to call.
2. **The caches take the log at construction.** `AltSvcCache` and `HstsCache` gain an optional
   `IDiagnosticLog` constructor parameter. Both are made per run or per transfer after the log is
   open: `AltSvcTransferCache.OpenAsync` and `HstsTransferPolicy` hand theirs on.
3. **Lines, all under ADR-0222's levels:**
   - `proxy`: `info` `using proxy <scheme>://<host>:<port> for <url-scheme>://<host>` - the proxy
     endpoint only, never its user information; `verbose` `<host> matches no-proxy entry
     '<entry>'; connecting directly` (the entry as written, found by the new
     `NoProxyMatcher.MatchingEntry`), `no proxy set for ...; connecting directly`, and `file URL
     never uses a proxy`. Unusable proxy text writes nothing: its exit 5 or 7 is the runner's line.
   - `altsvc`: `info` `using alternative <alpn> <host>:<port> for <alpn> <host>:<port>` for each
     match `FindForOrigin` returns; `verbose` `stored alternative ...` for each entry learned from
     a header and `no alternative for <host>:<port>` for a miss.
   - `hsts`: `verbose` `stored entry for <host>` for each header learned (new or updated) and
     `entry for <host> expired` for each expired entry a lookup removes; host names only.
4. Every caller that passes no log gets `NoDiagnosticLog.Instance`, so nothing changes without
   `--log-level`.

## Consequences

- One more optional parameter on `ProxySelector.TrySelect` and `TransferProxySelection.TrySelect`;
  existing callers compile unchanged.
- An HSTS header learned through `HstsTransferPolicy` writes two `verbose` lines: the cache's
  `stored entry` and the policy's `learned Strict-Transport-Security`. They say different things
  (the entry's life against the header's arrival) and both stay.
