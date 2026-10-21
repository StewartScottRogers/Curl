---
id: BL-2295
title: Define EasyNetworkOptions in Curl.Api.UnitLibrary with libcurl's defaults
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Options/Network, Curl.Api.UnitTests/Options/Network]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2295 — Define EasyNetworkOptions in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Network` (namespace `Curl.Api.Options.Network`) defines `EasyNetworkOptions`, an immutable record carrying every option of the plan's `EasyNetworkOptions` row (URL, protocols, ports, interfaces, connect-to and resolve, DNS, IP resolve, TCP, Unix sockets, connect timeouts, connection reuse, connect-only, upkeep, `USE_SSL`), each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506; plan section "CURLOPT groups". Follow the rules for option-group tasks written in BL-2294's Context (sealed record, enums in the same folder, `TimeSpan` for durations, XML doc naming each `CURLOPT_*` and its default from curl 8.22.0's `docs/libcurl/opts`, no protocol or engine reference). `CURLOPT_RESOLVE` and `CURLOPT_CONNECT_TO` keep their entry syntax as strings; parsing them stays the engine's job. BL-2303 retargets the engine onto this record.

## Acceptance criteria

- [x] `EasyNetworkOptions` exists in `Curl.Api.UnitLibrary/Options/Network` and carries every option in the plan's row, each documented with its `CURLOPT_*` name and default.
- [x] Tests in `Curl.Api.UnitTests/Options/Network` pin every default and that a `with` copy changes only the property set.
- [x] This task's Notes list each `CURLOPT_*` of the plan's row with the property that carries it (none left without one).
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

Defaults checked against curl 8.22.0 (`lib/url.c` `Curl_init_userdefined` and the
`docs/libcurl/opts` pages for CONNECTTIMEOUT, MAXLIFETIME_CONN, SERVER_RESPONSE_TIMEOUT and
ACCEPTTIMEOUT_MS). Notable: `MAXLIFETIME_CONN` is 24 hours since 8.17.0, `TCP_KEEPCNT` is 9,
`MAXAGE_CONN` is 118 s, `CONNECTTIMEOUT` is 300 s (0 means the same).

Choices (sensible defaults, rule 1):
- `CURLOPT_CURLU` has no handle type to carry yet (`ICurlUrlHandle` is BL-2314), so the record
  carries the handle's full URL as `UrlFromUrlHandle` (string), separate from `Url` because
  libcurl lets the handle win over `CURLOPT_URL`. BL-2314 may retype it.
- libcurl's NULL lists (`CONNECT_TO`, `RESOLVE`) default to empty lists; durations are `TimeSpan`,
  with `Timeout.InfiniteTimeSpan` for `DNS_CACHE_TIMEOUT`'s -1 and zero keeping libcurl's meaning.
- Ports and counts are `int`; `ADDRESS_SCOPE` is `long` (an unsigned long scope id in libcurl).
- `CONNECT_ONLY`, `IPRESOLVE` and `USE_SSL` get enums (`ConnectOnly`, `IpResolve`, `UseSsl`) with libcurl's numbers.

CURLOPT_* to property:
- URL -> Url; CURLU -> UrlFromUrlHandle; PATH_AS_IS -> PathAsIs
- PROTOCOLS_STR -> Protocols; REDIR_PROTOCOLS_STR -> RedirectProtocols; DEFAULT_PROTOCOL -> DefaultProtocol
- PORT -> Port; INTERFACE -> Interface; LOCALPORT -> LocalPort; LOCALPORTRANGE -> LocalPortRange
- CONNECT_TO -> ConnectTo; RESOLVE -> Resolve
- DNS_CACHE_TIMEOUT -> DnsCacheTimeout; DNS_SERVERS -> DnsServers; DNS_INTERFACE -> DnsInterface
- DNS_LOCAL_IP4 -> DnsLocalIPv4; DNS_LOCAL_IP6 -> DnsLocalIPv6; DNS_SHUFFLE_ADDRESSES -> DnsShuffleAddresses
- DOH_URL -> DohUrl; IPRESOLVE -> IpResolve
- TCP_NODELAY -> TcpNoDelay; TCP_FASTOPEN -> TcpFastOpen; TCP_KEEPALIVE -> TcpKeepAlive
- TCP_KEEPIDLE -> TcpKeepIdle; TCP_KEEPINTVL -> TcpKeepInterval; TCP_KEEPCNT -> TcpKeepCount
- ADDRESS_SCOPE -> AddressScope; UNIX_SOCKET_PATH -> UnixSocketPath; ABSTRACT_UNIX_SOCKET -> AbstractUnixSocket
- CONNECTTIMEOUT, CONNECTTIMEOUT_MS -> ConnectTimeout; HAPPY_EYEBALLS_TIMEOUT_MS -> HappyEyeballsTimeout
- FRESH_CONNECT -> FreshConnect; FORBID_REUSE -> ForbidReuse; MAXCONNECTS -> MaxConnects
- MAXAGE_CONN -> MaxIdleConnectionAge; MAXLIFETIME_CONN -> MaxConnectionLifetime
- CONNECT_ONLY -> ConnectOnly; ACCEPTTIMEOUT_MS -> AcceptTimeout; UPKEEP_INTERVAL_MS -> UpkeepInterval
- SERVER_RESPONSE_TIMEOUT, SERVER_RESPONSE_TIMEOUT_MS -> ServerResponseTimeout; USE_SSL -> UseSsl

Measured: Curl.Api.UnitLibrary 100% line, 100% branch, max complexity 1, 0 failing members;
Curl.Api.UnitTests 49 passed; full fast run green; `PassingUpstreamCases.txt` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyNetworkOptions carries the plan's network CURLOPT row with libcurl 8.22.0 defaults
