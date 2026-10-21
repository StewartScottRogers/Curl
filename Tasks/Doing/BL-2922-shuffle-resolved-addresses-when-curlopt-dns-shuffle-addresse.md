---
id: BL-2922
title: Shuffle resolved addresses when CURLOPT_DNS_SHUFFLE_ADDRESSES is set, and port upstream unit program 1608
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns/Shuffle, Curl.Conformance.UnitProgram.UnitTests/Dns/Shuffle, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-2922 — Shuffle resolved addresses when CURLOPT_DNS_SHUFFLE_ADDRESSES is set, and port upstream unit program 1608

## Goal

With `CURLOPT_DNS_SHUFFLE_ADDRESSES` on, Curl shuffles a host's resolved addresses before connecting, as libcurl's `dns_shuffle_addr` does, and upstream case 1608 runs as a ported unit program and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2847 (too big for one run). Today the option is only stored: `Libcurl/Options/Network/EasyNetworkOptionSetters.cs` sets `EasyNetworkOptions.DnsShuffleAddresses` (`Curl.Api.UnitLibrary/Options/Network/EasyNetworkOptions.cs`), and no code in `Curl.Engine.UnitLibrary` or `Curl.Networking.UnitLibrary` reads it (BL-2847 grep, 2026-10-21). Upstream: https://github.com/curl/curl/blob/curl-8_22_0/lib/hostip.c (`dns_shuffle_addr`, a Fisher-Yates shuffle with `Curl_rand`) and https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1608.c, which shuffles an 8-entry list up to 10 times and fails when the head never changes. Put the shuffle in its own testable member (inject the random source) in Networking and have the Engine's resolve step call it when the option is on; narrow `touches` to files per ADR-0694 once you know them. The port goes in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Shuffle/` (one `IUnitProgram`, as `UrlParsing/Unit1607ResolveParsing.cs`), registered by one added line in `UnitPrograms.Dns.cs` (union-merged), test in `Curl.Conformance.UnitProgram.UnitTests/Dns/Shuffle/`. Case 1608 needs feature `shuffle-dns`, which `UpstreamCurlPlatform` keeps off; report it for that case only (BL-2847 context) - if that needs a file outside `touches`, add it per the lane rules.

## Acceptance criteria

- [ ] With the option on, the resolved address list is shuffled (focused tests in the changed libraries' `.UnitTests`, random source injected); with it off, the order is unchanged.
- [ ] `unit1608` is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Shuffle/`, registered in `UnitPrograms.Dns.cs`, with a test; case 1608 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1608'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 6d74a273 5b3df23b dd1cc079.
