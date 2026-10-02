---
id: BL-1153
title: Cap DoH addresses and CNAMEs across both queries as curl's shared entry does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-958]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1153 — Cap DoH addresses and CNAMEs across both queries as curl's shared entry does

## Goal

`DohDnsResolver` stops storing addresses at 24 and CNAMEs at 4 across both DoH answers together, as measured from curl 8.21.0, not per answer as `DnsAnswerDecoder` does now.

## Context

- curl's `lib/doh.c` decodes the A and the AAAA answer into one `struct dohentry`, whose `DOH_MAX_ADDR` (24) and `DOH_MAX_CNAME` (4) limits count both answers. BL-958 made the entry shared for failed decodes and for `No content`; `DnsAnswerDecoder` still caps each answer on its own (`MaximumAddresses`, `MaximumCanonicalNames`).
- Measure first with `Record-CurlExchange.ps1 -Tls -Connections 2 -Response <A answer>,<AAAA answer>` (connection 1 serves the A query): e.g. 20 A records and 10 AAAA records, and 3 CNAMEs in each answer, under `-sS -v --trace-config doh --doh-url https://127.0.0.1:P/dns-query --doh-insecure http://example.test:P2/`.

## Acceptance criteria

- [ ] The measured `[DoH] A:`/`AAAA:`/`CNAME:` lines and the `IPv4:`/`IPv6:` lines for both cases are in Notes and pinned in `DohDnsResolverTraceTests`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
