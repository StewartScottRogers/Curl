# ADR-1313: With no version option, alt-svc ignores h2 and h3 entries on Windows

- Status: Accepted
- Date: 2026-11-19
- Task: BL-5574
- Decided by Claude under Stewart's delegation.
- Amends ADR-1309 (restores ADR-1238's Windows answer); supersedes ADR-1309 on Windows only.

## Context

ADR-1309 let a transfer with no version option switch to `h1`, `h2` and `h3` on every platform, because Curl's own `curl -V` (ADR-1282) lists HTTP2 and HTTP3, and it was "not measured against a live HTTP/3-capable curl". But the reference curl on Windows, 8.22.0 Schannel, is what Curl must match there, and its `curl -V` is measured again today: `Features: alt-svc AsynchDNS brotli Debug HSTS HTTPS-proxy IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd` - no HTTP2, no HTTP3. libcurl's `url_set_conn_peer` allows `h2` only `#ifdef USE_HTTP2` and `h3` only `#ifdef USE_HTTP3`, so that build allows `h1` alone. ADR-1238 recorded the outside review's findings STD-009 and STD-013, each reproduced at least twice: an `h2` entry and an `h3` entry both left the transfer on the origin.

## Decision

`AltSvcTransferCache.LookupOf` allows `h1` alone as the destination when there is no version option and the system is Windows, as ADR-1238 did; elsewhere it allows `h1`, `h2` and `h3` as ADR-1309 decided (the OpenSSL build with nghttp2 and ngtcp2). A version option keeps its destinations on every platform. Findings STD-009 and STD-013 stand, no longer superseded. The rule "match the platform's curl" outranks agreement with Curl's own `-V` listing.

## Consequences

On Windows an `h2` or `h3` entry never moves a transfer without a version option off the origin; entries are still read, learned and written. `AltSvcTransferCache.OpenAsync` takes `onWindows` again so tests pin either platform; `Curl.Engine.UnitTests` pins each answer under `[OSCondition]`.
