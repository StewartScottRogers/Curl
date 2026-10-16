# ADR-0515 — Mapping the command line's proxy group onto `EasyProxyOptions`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2305.

## Context

ADR-0506 has `Curl.Console/EasyOptionsFromCommandLine.cs` map a parsed command line onto the easy
option groups as the curl tool's `config2setopts.c` does; ADR-0513 and ADR-0514 settled the network
and TLS groups. BL-2305 adds the proxy group, `EasyProxyOptions`. `CommandLineOptions` keeps the
proxy as one `CommandLineProxy` (the last `-x`, `--proxy1.0` or `--socks*` value and the kind its
option names), `--proxy-http2` as a separate switch, and SOCKS5 authentication as two switches.

## Decision

- `Proxy` is the address as given, an empty `-x ''` included; `ProxyType` is the kind the last proxy
  option names, as `--libcurl` writes `CURLOPT_PROXYTYPE`. A scheme in the address is not read here:
  libcurl lets it outrank the type, so the record keeps what curl's tool sets.
- `--proxy-http2` makes an `-x` proxy `CURLPROXY_HTTPS2`, as curl's tool sets `proxyver`; it leaves
  a `--proxy1.0` or `--socks*` proxy's type alone, and with no proxy the type stays libcurl's default,
  as curl's tool sets `CURLOPT_PROXYTYPE` only with a proxy. curl 8.22.0 also lets a `--proxy-http2`
  given after `--socks5` turn that proxy into HTTPS2; `CommandLineOptions` does not keep the order, so
  the record follows the engine's behaviour (the SOCKS type), which wins under ADR-0506's retarget rule.
- `Socks5Authentication` is the `--socks5-basic` and `--socks5-gssapi` bits; with neither on (or both
  turned off again) it keeps libcurl's `CURLAUTH_BASIC | CURLAUTH_GSSAPI`, as curl's tool sets
  `CURLOPT_SOCKS5_AUTH` only when its bitmask is non-zero.
- `HeaderOption` stays `CURLHEADER_SEPARATE`: curl's tool only ever sets that value, which is libcurl's
  default. `ProxyPort` stays 0: curl's tool never sets `CURLOPT_PROXYPORT`.
- The proxy environment variables (`http_proxy`, `NO_PROXY`, ...) are libcurl's to read, so they stay
  in the engine and are not on the record. Proxy credentials and `--proxy-service-name` are
  `EasyAuthenticationOptions`', and proxy TLS is `EasyTlsOptions.Proxy` (ADR-0514).

## Consequences

`EasyOptionsFromCommandLine.ProxyOf` builds the whole proxy group, ready for the engine's proxy classes
to read in place of `CommandLineOptions` (BL-2325). Nothing reads it yet, so no behaviour changes.
