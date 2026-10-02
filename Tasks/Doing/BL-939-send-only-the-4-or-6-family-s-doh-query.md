---
id: BL-939
title: Send only the -4 or -6 family's DoH query
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-642]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-939 — Send only the -4 or -6 family's DoH query

## Goal

Under `-4` `DohDnsResolver` POSTs only the A query, and under `-6` only the AAAA query, as curl 8.21.0 does, instead of both.

## Context

- Measured in BL-642 (ADR-0152's BL-642 amendment, point 5): `curl -sS -4 --doh-url https://127.0.0.1:P1/dns-query --doh-insecure http://example.test:P2/` sent one 153-byte POST (QTYPE A) to the DoH recorder, and `-6` one POST with QTYPE AAAA (28).
- Today `DohDnsResolver` (`Curl.Networking.UnitLibrary/DohDnsResolver.cs`) always sends both; `TcpConnector` then filters the addresses by family, so the transfer's output is already right and only the DoH server sees the extra query.
- Give `DohDnsResolver` an `AddressFamily` (as `DnsServerResolverOptions` carries one for the c-ares resolver) and pass `CurlComposition.AddressFamilyOf(options)` in `CurlComposition.CreateDohResolver`.

## Acceptance criteria

- [ ] A `DohDnsResolverTests` test pins that with `AddressFamily.InterNetwork` exactly one connection is opened and its POST carries QTYPE `00 01`, and with `AddressFamily.InterNetworkV6` QTYPE `00 1C`; `Unspecified` still sends both, A first.
- [ ] A `CurlCompositionDohTests` test pins that `-4 --doh-url ...` writes only the A POST to the scripted DoH server.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
