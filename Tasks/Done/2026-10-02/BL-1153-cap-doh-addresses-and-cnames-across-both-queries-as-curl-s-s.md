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
completed: 2026-10-02
---
# BL-1153 — Cap DoH addresses and CNAMEs across both queries as curl's shared entry does

## Goal

`DohDnsResolver` stops storing addresses at 24 and CNAMEs at 4 across both DoH answers together, as measured from curl 8.21.0, not per answer as `DnsAnswerDecoder` does now.

## Context

- curl's `lib/doh.c` decodes the A and the AAAA answer into one `struct dohentry`, whose `DOH_MAX_ADDR` (24) and `DOH_MAX_CNAME` (4) limits count both answers. BL-958 made the entry shared for failed decodes and for `No content`; `DnsAnswerDecoder` still caps each answer on its own (`MaximumAddresses`, `MaximumCanonicalNames`).
- Measure first with `Record-CurlExchange.ps1 -Tls -Connections 2 -Response <A answer>,<AAAA answer>` (connection 1 serves the A query): e.g. 20 A records and 10 AAAA records, and 3 CNAMEs in each answer, under `-sS -v --trace-config doh --doh-url https://127.0.0.1:P/dns-query --doh-insecure http://example.test:P2/`.

## Acceptance criteria

- [x] The measured `[DoH] A:`/`AAAA:`/`CNAME:` lines and the `IPv4:`/`IPv6:` lines for both cases are in Notes and pinned in `DohDnsResolverTraceTests`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-02, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Tls -Connections 2 -Response <A answer>,<AAAA answer>` under `-sS -v --trace-config doh -m 3 --doh-url https://127.0.0.1:48911/dns-query --doh-insecure http://example.test:48799/`, every record TTL 60:
  - Case 1, A answer `127.0.0.1`-`.20` + CNAMEs `c1`-`c3.test`, AAAA answer `::1`-`::a` + CNAMEs `c4`-`c6.test`: `[DNS] hostname: example.test`, `[DoH] TTL: 60 seconds`, `[DoH] A: 127.0.0.1` ... `[DoH] A: 127.0.0.20`, `[DoH] AAAA: 0000:...:0001` ... `[DoH] AAAA: 0000:...:0004`, `CNAME: c1.test` ... `CNAME: c4.test`; then `IPv6: ::1, ::2, ::3, ::4` and `IPv4: 127.0.0.1, ..., 127.0.0.20`.
  - Case 2, A answer `127.0.0.1`-`.24` + CNAMEs `c1`-`c4.test`, AAAA answer `::1`-`::5` + CNAMEs `c5`-`c6.test`: the 24 `[DoH] A:` lines and `CNAME: c1.test` ... `c4.test`, no AAAA line; then `IPv6: (none)` and the 24 IPv4 addresses.
  - Pinned in `DohDnsResolverTraceTests.ResolveAsync_AddressesAndCnamesOverTheSharedLimit_KeepsTheFirst24AddressesAnd4Cnames` and `ResolveAsync_AFillingTheSharedLimit_KeepsNothingFromTheAaaaAnswer`, the resolved list standing for the `IPv4:`/`IPv6:` lines (TcpConnector prints them from it).
- Implementation: `DohQueryResult.AsOneEntry` now cuts each answer, A first, to the room the earlier answers left in the shared entry (24 addresses, 4 CNAMEs). `DnsAnswerDecoder` keeps its own per-answer caps, which `DnsServerResolver` still uses.
- Choice (sensible default): the cut is applied after each answer is decoded on its own. One case differs from curl: when the entry already holds 4 CNAMEs, curl does not follow a later answer's CNAME name at all, so a malformed CNAME there would not fail its decode, while ours still does. Left as is; no measured case reaches it.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Networking.UnitTests 2736 passed); `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. DoH answers share curl's 24-address and 4-CNAME entry limit across the A and AAAA queries
