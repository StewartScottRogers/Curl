---
id: BL-2970
title: Decode DoH CNAME answers as curl 8.22.0 does and write its A and AAAA --trace-config doh lines
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs, Curl.Networking.UnitLibrary/DnsAnswer.cs, Curl.Networking.UnitLibrary/DnsMessageFailure.cs, Curl.Networking.UnitLibrary/DohQueryResult.cs, Curl.Networking.UnitLibrary/DohTraceLines.cs, Curl.Networking.UnitLibrary/DnsServerQuery.cs, Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitTests/DnsAnswerDecoderTests.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs, Curl.Networking.UnitTests/NetworkingAdversarialTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2970 — Decode DoH CNAME answers as curl 8.22.0 does and write its A and AAAA --trace-config doh lines

## Goal

`DnsAnswerDecoder.Decode` skips CNAME and DNAME data and never reports `NoContent`, as curl 8.22.0's `doh_resp_decode` does, and `DohDnsResolver` writes the A and AAAA queries' `-v --trace-config doh` lines in curl 8.22.0's form, measured.

## Context

Split from BL-2969 (ADR-0738), which moved RCODE 3 to `NxDomain` ("No such name") and capped answer TTLs at 86400. curl 8.22.0's `lib/vdns/doh.c` (tag `curl-8_22_0`):

- `doh_rdata` stores only A, AAAA and HTTPS data; a CNAME (and a DNAME) is skipped, so unit1650's self-pointing CNAME row (46 bytes, `\x07\x03any\xc0\x27\x00`) decodes `DOH_OK` where Curl's decoder follows it to `LabelLoop`. There is no "no content" result: an answer with no address is `DOH_OK`. Keep SRV records for the c-ares path (`DnsServerQuery`/`DnsServerResolver`), which 8.22.0's DoH never asks; `DnsServerQuery.Read` must keep its measured c-ares outcomes (`NoData` for an answer without a record of the type asked for).
- The resolver's trace is no longer 8.21.0's `[DNS] DoH: <failure> type A for <host>` plus a `[DNS] hostname:` / `[DoH] TTL:` / `[DoH] A:` / `CNAME:` entry. Per probe 8.22.0 writes `[DoH] [A] error: <curl_easy_strerror>`, `[DoH] [A] error: HTTP status <n>` (a non-2xx status is now a failure, `DOH_HTTP_FAILED`), `[DoH] [A] error decoding response: <doh_strerror>`, or `DoH request complete, <n> to go`. Measure what else it prints (`Curl_dnsc_mk_addr2`, `lib/vdns/cf-dns.c`) with the local curl 8.22.0 and `Record-CurlExchange.ps1 -Tls` before pinning, for: an A answer, an NXDOMAIN answer, a CNAME-only answer, an empty answer, a 500 with an empty body.
- Then drop or rework what only 8.21.0 needed: `DnsAnswer.CanonicalNames`, `DnsMessageFailure.NoContent`, `DohQueryResult.AsOneEntry`'s NoContent rule and CNAME room, `DohTraceLines`' `CNAME:` lines. The exit code stays 6 when no query gave an address (`Curl_doh_take_result`).
- BL-2954 (the HTTPS-RR query's lines) also touches `DohTraceLines` and `DohDnsResolver`: whichever runs second builds on the other's line format.

If too big for one run, split it: the decoder change (CNAME skipped, no NoContent) first, the trace-line format second.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` pins that `DnsAnswerDecoder.Decode` gives `None` for unit1650's self-pointing CNAME row and for an answer with no record.
- [ ] An ADR (or an amendment to ADR-0738) records curl 8.22.0's measured `-v --trace-config doh` lines and exit code for an A answer, an NXDOMAIN answer, a CNAME-only answer and an empty answer, and `DohDnsResolverTraceTests` pins them.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
