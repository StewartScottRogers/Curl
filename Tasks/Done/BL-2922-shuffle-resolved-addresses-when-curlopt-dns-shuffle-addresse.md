---
id: BL-2922
title: Shuffle resolved addresses when CURLOPT_DNS_SHUFFLE_ADDRESSES is set, and port upstream unit program 1608
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns/Shuffle, Curl.Conformance.UnitProgram.UnitTests/Dns/Shuffle, Curl.Networking.UnitLibrary/DnsAddressShuffle.cs, Curl.Networking.UnitLibrary/ShufflingDnsResolver.cs, Curl.Networking.UnitTests/DnsAddressShuffleTests.cs, Curl.Networking.UnitTests/ShufflingDnsResolverTests.cs, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests/EngineCompositionDnsShuffleTests.cs, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2922 — Shuffle resolved addresses when CURLOPT_DNS_SHUFFLE_ADDRESSES is set, and port upstream unit program 1608

## Goal

With `CURLOPT_DNS_SHUFFLE_ADDRESSES` on, Curl shuffles a host's resolved addresses before connecting, as libcurl's `dns_shuffle_addr` does, and upstream case 1608 runs as a ported unit program and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2847 (too big for one run). Today the option is only stored: `Libcurl/Options/Network/EasyNetworkOptionSetters.cs` sets `EasyNetworkOptions.DnsShuffleAddresses` (`Curl.Api.UnitLibrary/Options/Network/EasyNetworkOptions.cs`), and no code in `Curl.Engine.UnitLibrary` or `Curl.Networking.UnitLibrary` reads it (BL-2847 grep, 2026-10-21). Upstream: https://github.com/curl/curl/blob/curl-8_22_0/lib/hostip.c (`dns_shuffle_addr`, a Fisher-Yates shuffle with `Curl_rand`) and https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1608.c, which shuffles an 8-entry list up to 10 times and fails when the head never changes. Put the shuffle in its own testable member (inject the random source) in Networking and have the Engine's resolve step call it when the option is on; narrow `touches` to files per ADR-0694 once you know them. The port goes in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Shuffle/` (one `IUnitProgram`, as `UrlParsing/Unit1607ResolveParsing.cs`), registered by one added line in `UnitPrograms.Dns.cs` (union-merged), test in `Curl.Conformance.UnitProgram.UnitTests/Dns/Shuffle/`. Case 1608 needs feature `shuffle-dns`, which `UpstreamCurlPlatform` keeps off; report it for that case only (BL-2847 context) - if that needs a file outside `touches`, add it per the lane rules.

## Acceptance criteria

- [x] With the option on, the resolved address list is shuffled (focused tests in the changed libraries' `.UnitTests`, random source injected); with it off, the order is unchanged.
- [x] `unit1608` is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Shuffle/`, registered in `UnitPrograms.Dns.cs`, with a test; case 1608 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1608'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 2026-10-21 (lane 3): `touches` narrowed from the whole Networking and Engine projects to the files changed (ADR-0694), and `Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs` with its tests added for the case-1608-only `shuffle-dns` feature (no task in Doing named them).
- Upstream's shuffle is in `lib/vdns/dnscache.c` at curl-8_22_0 (not `hostip.c`): `dns_shuffle_addr` runs in `dnsc_entry_assign_addr`, before the entry is cached.
- Built: `Curl.Networking`'s `DnsAddressShuffle` (Fisher-Yates, random source injected, `RandomNumberGenerator.GetInt32` in production) and `ShufflingDnsResolver`; `EngineComposition.LookUpsOf` wraps the connectors' resolver when `DnsShuffleAddresses` is set; `Unit1608DnsShuffle` ported and registered; `UpstreamCaseScreening` reports `shuffle-dns` to unit1608's case alone. ADR-0740 records the choices.
- `--resolve` entries skip the resolver and are not shuffled yet: filed as BL-2975.
- Measured: case 1608 passes with `CURL_UPSTREAM_CASE=1608`; fast tests green; `Measure-CodeQuality.ps1` over Networking, Engine, Conformance.UnitProgram and Conformance: 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 6d74a273 5b3df23b dd1cc079.
- 2026-10-21: Doing -> Done. CURLOPT_DNS_SHUFFLE_ADDRESSES shuffles each looked-up address list; upstream unit1608 ported and case 1608 on the ratchet
