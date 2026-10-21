---
id: BL-2977
title: Shuffle --resolve entries' addresses when CURLOPT_DNS_SHUFFLE_ADDRESSES is set
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/TcpConnectorResolveShuffleTests.cs, Curl.Engine.UnitLibrary/EngineComposition.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2977 — Shuffle --resolve entries' addresses when CURLOPT_DNS_SHUFFLE_ADDRESSES is set

## Goal

With `CURLOPT_DNS_SHUFFLE_ADDRESSES` on, the addresses of a `CURLOPT_RESOLVE` (`--resolve`) entry are shuffled as they enter the DNS cache, as curl 8.22.0 shuffles every address entry it makes.

## Context

BL-2922 (ADR-0740) shuffles each look-up's answer through `Curl.Networking`'s `ShufflingDnsResolver`, which `EngineComposition.LookUpsOf` puts under the connectors when the option is on. `--resolve` entries skip the resolver: `TcpConnector.LoadResolveEntry` stores `entry.Addresses` straight into the cache. Upstream (https://github.com/curl/curl/blob/curl-8_22_0/lib/vdns/dnscache.c, `dnsc_entry_assign_addr`) calls `dns_shuffle_addr` for every address entry when `data->set.dns_shuffle_addresses` is set. Give `TcpConnector` an optional shuffle (e.g. a `Func<IReadOnlyList<IPAddress>, IReadOnlyList<IPAddress>>` built on `DnsAddressShuffle.Shuffle` with an injected random source) that `EngineComposition.CreateTcpConnector` sets from `EasyNetworkOptions.DnsShuffleAddresses`, applied in `LoadResolveEntry` before `Store`. The `Added H:P:A to DNS cache` line keeps the entry's text as written.

## Acceptance criteria

- [ ] With the option on, a `--resolve` entry's cached addresses are shuffled (a focused test in `Curl.Networking.UnitTests` with the random source injected); with it off, they keep the written order.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 0e1c93ab dd1cc079.
