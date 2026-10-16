# ADR-0636 — `Curl.Api`'s transport option records: one to one onto the seam, validated when set, a request's record replacing the client's

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2502.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md` asks for connection, DNS, timeout, TLS and
proxy settings in `Curl.Api`, mapped onto the seam's `EasyNetworkOptions`, `EasyTlsOptions` and
`EasyProxyOptions`, settable per client and per request with the request winning, and rejecting an
invalid value before any transfer.

## Decision

- `CurlNetworkOptions`, `CurlTlsOptions` (with `CurlTlsPeerOptions` for `Origin` and `Proxy`) and
  `CurlProxyOptions` are records in `Curl.Api/Options/Transport`, namespace `Curl.Api`, whose members
  carry the seam members' names, types and libcurl 8.22.0 defaults, one to one. The seam's enums
  (`IpResolve`, `UseSsl`, `SslVersion`, `SslVersionMaximum`, `SslOptions`, `ProxyType`,
  `HeaderOption`, `Socks5Authentication`) are reused, not copied: they are already public, .NET-named
  and one to one with libcurl's values.
- Left out of `CurlNetworkOptions`: `Url` and `UrlHandle`, because `CurlRequest.Url` owns the URL, and
  `ConnectOnly`, because `CurlClient.ConnectWebSocketAsync` owns it. Every other seam member is mapped.
- `CurlNetworkOptions.ConnectTimeout` is `TimeSpan?`: `null` keeps whatever connect timeout is already
  in place (`CurlClientOptions.ConnectTimeout`, or the client's record), so the existing client
  setting and the record do not fight.
- Each validated member checks its value in its `init` accessor and throws
  `ArgumentOutOfRangeException` (a negative time; a port outside 0 to 65535; a value outside libcurl's
  range for `LocalPortRange`, `IpTypeOfService`, `VlanPriority`, counts and scope; an enum value
  libcurl does not name; a flag bit it does not define) or `ArgumentNullException` (a list or peer
  record set to `null`). An invalid record cannot be built, so no transfer ever starts with one.
  `Timeout.InfiniteTimeSpan` is accepted where libcurl takes -1 (`DnsCacheTimeout`,
  `CertificateAuthorityCacheTimeout`).
- `CurlClientOptions` and `CurlRequest` each gain nullable `Network`, `Tls` and `Proxy`. `CreateEasy`
  applies the client's records after its other defaults; `SendAsync` then applies the request's. A
  record that is set replaces its whole group (only the URL and connect-only mode survive in the
  network group); a record left `null` keeps the group as it was.

## Why

- Whole-record replacement keeps each mapping a straight copy with no branch per member, so the
  mapping stays inside the complexity gate and the rule is easy to state: a request that sets
  `Tls` gets exactly that `Tls`. Member-by-member merging would need every member nullable and a
  `??` per member, doubling the branches to cover for no behaviour libcurl has (libcurl's own
  handles carry one value per option, set or not).
- Validating in `init` throws at the line that set the bad value, before any handle is created.

## Consequences

- A request that wants one TLS setting changed from the client's builds its record from the client's:
  `client.Options.Tls! with { ... }`.
