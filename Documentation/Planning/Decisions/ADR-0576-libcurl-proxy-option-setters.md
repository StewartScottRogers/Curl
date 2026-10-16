# ADR-0576 — `Libcurl`'s proxy option setters check values as curl 8.22.0's `setopt.c` does, and copy `PROXYHEADER`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2405.

## Context

BL-2405 adds a setter row (ADR-0560) for every `CURLOPT_*` that `EasyProxyOptions` holds: `PROXY`,
`PROXYPORT`, `PROXYTYPE`, `PRE_PROXY`, `NOPROXY`, `HTTPPROXYTUNNEL`, `SUPPRESS_CONNECT_HEADERS`,
`PROXYHEADER`, `HEADEROPT`, `SOCKS5_AUTH`, `SOCKS5_GSSAPI_NEC`, `SOCKS5_GSSAPI_SERVICE`,
`HAPROXYPROTOCOL` and `HAPROXY_CLIENT_IP`. `PROXY_SERVICE_NAME`, which libcurl stores in the same
string as `SOCKS5_GSSAPI_SERVICE`, has no field here; it belongs to the authentication options.

## Decision

1. The rows live in `Libcurl/Options/Proxy`. `EasyOptionSetters.AddProxySetters` calls
   `EasyProxyOptionSetters.AddTo`, one named static method per row, as ADR-0572 does.
2. Checks follow `setopt.c`, leaving the options unchanged on a refusal: `PROXYPORT` outside 0..65535
   and `PROXYTYPE` outside `CURLPROXY_HTTP`..`CURLPROXY_SOCKS5_HOSTNAME` are `BadFunctionArgument`;
   `SOCKS5_AUTH` with any bit other than `CURLAUTH_BASIC` and `CURLAUTH_GSSAPI` is `NotBuiltIn`.
3. `HEADEROPT` keeps only the `CURLHEADER_SEPARATE` bit, as `setopt.c`'s `!!(arg & CURLHEADER_SEPARATE)`
   does, so any value without it is `CURLHEADER_UNIFIED`.
4. `HAPROXY_CLIENT_IP` also turns `HAPROXYPROTOCOL` on, for `null` too, as `setopt.c` does.
5. `PROXYHEADER` keeps a copy of the list made at set time, as `HTTPHEADER` does in `Options/Http`, so
   a caller changing its own list afterwards changes nothing; `null` empties it. libcurl keeps the
   caller's `curl_slist` pointer, which a managed list cannot safely share.
6. The on/off options are on for any value but 0; the strings are stored as given and `null` clears
   them.

## Consequences

- `SOCKS5_GSSAPI_SERVICE` and a future `PROXY_SERVICE_NAME` row set different fields, where libcurl
  sets one string; the engine decides which wins when it uses them.
