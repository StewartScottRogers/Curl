---
id: BL-2298
title: Define EasyProxyOptions in Curl.Api.UnitLibrary with libcurl's defaults
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Options/Proxy, Curl.Api.UnitTests/Options/Proxy]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2298 — Define EasyProxyOptions in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Proxy` (namespace `Curl.Api.Options.Proxy`) defines `EasyProxyOptions`, an immutable record carrying every option of the plan's `EasyProxyOptions` row (proxy, port, type, pre-proxy, no-proxy, tunnelling, CONNECT headers, SOCKS5 auth and GSS-API, HAProxy protocol), each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506; plan section "CURLOPT groups". Follow the rules for option-group tasks written in BL-2294's Context. `PROXYTYPE` becomes an enum of `CURLPROXY_*`; `SOCKS5_AUTH` a `[Flags]` enum of the `CURLAUTH_*` bits it accepts; `HEADEROPT` an enum of `CURLHEADER_*`. Proxy credentials belong to `EasyAuthenticationOptions` (BL-2297) and proxy TLS to `EasyTlsOptions` (BL-2296); do not duplicate them. BL-2305 retargets the engine onto this record.

## Acceptance criteria

- [x] `EasyProxyOptions` exists in `Curl.Api.UnitLibrary/Options/Proxy` and carries every option in the plan's row, each documented with its `CURLOPT_*` name and default.
- [x] Tests in `Curl.Api.UnitTests/Options/Proxy` pin every default and enum value, and that a `with` copy changes only the property set.
- [x] This task's Notes list each `CURLOPT_*` of the plan's row with the property that carries it (none left without one).
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

Shape: one `EasyProxyOptions` record; enums `ProxyType` (`CURLPROXY_*`), `HeaderOption` (`CURLHEADER_*`) and `[Flags] Socks5Authentication` (`CURLAUTH_NONE`, `BASIC`, `GSSAPI`), all with libcurl's numeric values.

Choices (sensible defaults, no ADR needed):
- `PROXYPORT` is an `int`, default 0 (the proxy scheme's default port), the option's literal default.
- `PROXYHEADER` is `IReadOnlyList<string>`, default empty, as `EasyNetworkOptions` does for its lists.
- `SOCKS5_GSSAPI_SERVICE` defaults to `null` (libcurl then uses `rcmd`), like the TLS group's "library's own" strings.
- `NOPROXY` stays a single comma-separated string, as libcurl takes it.

CURLOPT -> property:
- `PROXY` -> `EasyProxyOptions.Proxy`
- `PROXYPORT` -> `EasyProxyOptions.ProxyPort`
- `PROXYTYPE` -> `EasyProxyOptions.ProxyType`
- `PRE_PROXY` -> `EasyProxyOptions.PreProxy`
- `NOPROXY` -> `EasyProxyOptions.NoProxy`
- `HTTPPROXYTUNNEL` -> `EasyProxyOptions.HttpProxyTunnel`
- `SUPPRESS_CONNECT_HEADERS` -> `EasyProxyOptions.SuppressConnectHeaders`
- `PROXYHEADER` -> `EasyProxyOptions.ProxyHeaders`
- `HEADEROPT` -> `EasyProxyOptions.HeaderOption`
- `SOCKS5_AUTH` -> `EasyProxyOptions.Socks5Authentication`
- `SOCKS5_GSSAPI_NEC` -> `EasyProxyOptions.Socks5GssApiNec`
- `SOCKS5_GSSAPI_SERVICE` -> `EasyProxyOptions.Socks5GssApiService`
- `HAPROXYPROTOCOL` -> `EasyProxyOptions.HaProxyProtocol`
- `HAPROXY_CLIENT_IP` -> `EasyProxyOptions.HaProxyClientIp`

Measured: `Measure-CodeQuality.ps1 -Library Curl.Api.UnitLibrary` -> 100% line, 100% branch, 0 failing members, worst CRAP 1. Curl.Api.UnitTests: 75 tests pass; full fast run green; `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyProxyOptions carries every proxy CURLOPT with libcurl 8.22.0's defaults
