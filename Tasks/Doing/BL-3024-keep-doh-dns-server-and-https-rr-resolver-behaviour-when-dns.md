---
id: BL-3024
title: Keep DoH, DNS-server and HTTPS-RR resolver behaviour when DNS shuffle wraps the resolver
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/ShufflingDnsResolver.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-3024 — Keep DoH, DNS-server and HTTPS-RR resolver behaviour when DNS shuffle wraps the resolver

## Goal

With `CURLOPT_DNS_SHUFFLE_ADDRESSES` set, `TcpConnector` still sends the DoH HTTPS-RR query, still prints the DoH lookup failure once, and still caches a DNS-server resolver's failed lookups, exactly as it does without the shuffle.

## Context

Found by the shift-end review of 2026-10-21 (ADR-0710), pr-review-toolkit code-reviewer, in the diff 3d2c9775d...9795414d7. Introduced by 4e184d6e7 (BL-2922).

- `Curl.Engine.UnitLibrary/EngineComposition.Dns.cs:58-59` (`LookUpsOf`) wraps the picked resolver in `ShufflingDnsResolver` (`Curl.Networking.UnitLibrary/ShufflingDnsResolver.cs`) when `DnsShuffleAddresses` is set, and `EngineComposition.Transports.cs:127-135` hands that wrapper to `TcpConnector`.
- `TcpConnector` branches on its resolver's concrete type, and the wrapper hides it:
  - `TcpConnector.cs:1139`, `dnsResolver is not DohDnsResolver`: the duplicate `Could not resolve host` info line is printed for a DoH lookup failure.
  - `TcpConnector.cs:1195`, `dnsResolver is DnsServerResolver`: failed `--dns-servers` lookups stop being cached as negative entries (ADR-0499).
  - `TcpConnector.cs:1239`, `dnsResolver is IHttpsOriginDnsResolver`: the HTTPS-RR query is never sent, so no `HTTPS-RR` trace line and no HTTPS record cache entry.
- Each branch works when the shuffle is off. No test combines the shuffle with DoH or `--dns-servers`.
- One possible fix: give the wrapper a way to expose the resolver it wraps, and have `TcpConnector` test that one. A shuffle decorator that also forwards `IHttpsOriginDnsResolver` would also work.

## Acceptance criteria

- [ ] A `Curl.Networking.UnitTests` test shows a `TcpConnector` over `ShufflingDnsResolver` wrapping a `DohDnsResolver` sends the HTTPS-RR query for an `https://` origin, as it does unwrapped.
- [ ] A test shows a DoH lookup failure through the shuffle wrapper gives the same trace lines as without it.
- [ ] A test shows a `DnsServerResolver` failure through the shuffle wrapper is cached as a negative DNS entry.
- [ ] `dotnet build` is clean and the fast tests pass; the 100% coverage gates still hold for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: caaca4ed 4d363ca1 6aad2ab5 3d0d2435.
