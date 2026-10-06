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
completed: 2026-10-02
---
# BL-958 — Keep a failed DoH decode's CNAMEs, addresses and TTL in the trace entry as curl's shared entry does

## Goal

`DohDnsResolver`'s `--trace-config doh` entry (`[DoH] TTL:`, `[DoH] A:`/`AAAA:`, `CNAME:`) includes what a failed decode read before it stopped, as curl 8.21.0 does: for a `CNAME example.test -> a.test` then `AAAA ::1` answer given to both queries it prints `CNAME: a.test` twice.

## Context

- curl's `lib/doh.c` decodes both queries into one `struct dohentry`, so records a decode read before it failed (e.g. `Unexpected TYPE` on the A query meeting the AAAA record) stay in the entry. `DnsAnswerDecoder` empties `Addresses` and `CanonicalNames` on failure (it keeps `TimeToLiveSeconds`), so BL-850's `DohTraceLines` prints the CNAME once.
- Measured on 2026-09-29 (BL-850, `Record-CurlExchange.ps1 -Tls -Connections 2`, `-sS -v --trace-config doh --doh-url https://127.0.0.1:P/dns-query --doh-insecure http://example.test:48799/`): `[DNS] DoH: Unexpected TYPE type A for example.test`, `[DNS] hostname: example.test`, `[DoH] TTL: 30 seconds`, `[DoH] AAAA: 0000:0000:0000:0000:0000:0000:0000:0001`, `CNAME: a.test`, `CNAME: a.test`. The answer bytes are `MeasuredCnameAndAaaaAnswer` in `Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs`.
- Measure whether a leaked address from a failed decode also reaches the resolved addresses (`IPv4:`/`IPv6:` lines) before changing what `ResolveAsync` returns.

## Acceptance criteria

- [x] `DohDnsResolverTraceTests.ResolveAsync_ACnameAndAaaaAnswer_...` pins `CNAME: a.test` twice, as measured.
- [x] Whether a failed decode's addresses reach the resolved list is measured and pinned in a test, with the measurement in Notes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-10-02, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Tls -Connections 2 -Response <r1>,<r2>`
(connection 1 serves the A query, confirmed by the order of the outcome), `-sS -v --trace-config doh
--doh-url https://127.0.0.1:P/dns-query --doh-insecure http://example.test:48799/`:

- A query: `A 127.0.0.2` (TTL 60) then `AAAA ::1`; AAAA query: `AAAA ::1` (TTL 90). Trace:
  `DoH: Unexpected TYPE type A`, `hostname: example.test`, `TTL: 60 seconds`, `[DoH] A: 127.0.0.2`,
  `[DoH] AAAA: 0000:...:0001`; then `IPv6: ::1`, `IPv4: 127.0.0.2`, and curl tried both, exit 7.
  So a failed decode's address does reach the resolved list
  (`ResolveAsync_AFailedADecodeAfterAnAddress_ReportsAndResolvesThatAddress`).
- A query: A then AAAA; AAAA query: AAAA then A. Both `Unexpected TYPE` lines, no entry, exit 6:
  with no query answered nothing resolves, leaked addresses included.
- A query: `CNAME a.test`; AAAA query: no records. No `No content` line; entry `TTL: 30`, `CNAME: a.test`.
  The reverse (A empty, AAAA the CNAME) prints `DoH: No content type A`. curl decodes A first into
  the shared entry, so its `No content` check sees what an earlier answer stored.

Design: `DnsAnswerDecoder` now keeps a failed decode's addresses and CNAMEs (its TTL it already kept;
SRV and HTTPS data stay empty on failure, and `DnsServerQuery` only reads a `None` answer, so the
`--dns-servers` path is unchanged). `DohQueryResult.Addresses` returns them; `DohQueryResult.AsOneEntry`
turns a `NoContent` answer into a decoded one when an earlier answer filled the entry; `ResolveAsync`
returns addresses only when some query counts as answered. The behaviour is measured curl, so no ADR.
Not done here: curl's 24-address and 4-CNAME limits count both answers together; filed as BL-1153.

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A failed DoH decode's CNAMEs and addresses stay in the trace entry and resolved list, and No content sees the shared entry, as curl 8.21.0
