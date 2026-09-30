---
id: BL-958
title: Keep a failed DoH decode's CNAMEs, addresses and TTL in the trace entry as curl's shared entry does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-850]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-958 — Keep a failed DoH decode's CNAMEs, addresses and TTL in the trace entry as curl's shared entry does

## Goal

`DohDnsResolver`'s `--trace-config doh` entry (`[DoH] TTL:`, `[DoH] A:`/`AAAA:`, `CNAME:`) includes what a failed decode read before it stopped, as curl 8.21.0 does: for a `CNAME example.test -> a.test` then `AAAA ::1` answer given to both queries it prints `CNAME: a.test` twice.

## Context

- curl's `lib/doh.c` decodes both queries into one `struct dohentry`, so records a decode read before it failed (e.g. `Unexpected TYPE` on the A query meeting the AAAA record) stay in the entry. `DnsAnswerDecoder` empties `Addresses` and `CanonicalNames` on failure (it keeps `TimeToLiveSeconds`), so BL-850's `DohTraceLines` prints the CNAME once.
- Measured on 2026-09-29 (BL-850, `Record-CurlExchange.ps1 -Tls -Connections 2`, `-sS -v --trace-config doh --doh-url https://127.0.0.1:P/dns-query --doh-insecure http://example.test:48799/`): `[DNS] DoH: Unexpected TYPE type A for example.test`, `[DNS] hostname: example.test`, `[DoH] TTL: 30 seconds`, `[DoH] AAAA: 0000:0000:0000:0000:0000:0000:0000:0001`, `CNAME: a.test`, `CNAME: a.test`. The answer bytes are `MeasuredCnameAndAaaaAnswer` in `Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs`.
- Measure whether a leaked address from a failed decode also reaches the resolved addresses (`IPv4:`/`IPv6:` lines) before changing what `ResolveAsync` returns.

## Acceptance criteria

- [ ] `DohDnsResolverTraceTests.ResolveAsync_ACnameAndAaaaAnswer_...` pins `CNAME: a.test` twice, as measured.
- [ ] Whether a failed decode's addresses reach the resolved list is measured and pinned in a test, with the measurement in Notes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
