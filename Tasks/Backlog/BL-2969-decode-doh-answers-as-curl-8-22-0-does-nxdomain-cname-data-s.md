---
id: BL-2969
title: Decode DoH answers as curl 8.22.0 does: NXDOMAIN, CNAME data skipped, TTL capped at 86400
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs, Curl.Networking.UnitLibrary/DnsAnswer.cs, Curl.Networking.UnitLibrary/DnsMessageFailure.cs, Curl.Networking.UnitLibrary/DnsMessageFailureText.cs, Curl.Networking.UnitLibrary/DohQueryResult.cs, Curl.Networking.UnitLibrary/DohTraceLines.cs, Curl.Networking.UnitLibrary/DnsServerQuery.cs, Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-2969 — Decode DoH answers as curl 8.22.0 does: NXDOMAIN, CNAME data skipped, TTL capped at 86400

## Goal

`DnsAnswerDecoder.Decode` returns what curl 8.22.0's `doh_resp_decode` returns for every response row of upstream `tests/unit/unit1650.c`, and the DoH resolver's outcome and `--trace-config doh` lines follow from it as curl 8.22.0's do.

## Context

Found while porting unit1650 (BL-2923). Curl's decoder follows curl 8.21.0; curl 8.22.0's `lib/vdns/doh.c` (https://github.com/curl/curl/blob/curl-8_22_0/lib/vdns/doh.c, `doh_resp_decode` and `doh_rdata`, lines 581-768) differs in three ways that unit1650 checks:

1. RCODE 3 returns `DOH_DNS_NXDOMAIN`, not `DOH_DNS_BAD_RCODE` (resp row 3, `\x00\x00\x00\x03...`). Curl has no `NxDomain` member in `DnsMessageFailure`. The resolver also treats NXDOMAIN apart (`doh.c` around lines 1093 and 1178-1205: an NXDOMAIN probe vs a SERVFAIL-class one, and "only NXDOMAIN answers for all of them"); measure what curl 8.22.0 prints and exits with for an NXDOMAIN answer, and the `doh_strerror` text, before pinning.
2. CNAME (and DNAME) record data is not parsed or stored at all (`doh_rdata`'s default case), so a CNAME whose name points at itself decodes `DOH_OK` (resp row 11, 46 bytes, `\x07\x03any\xc0\x27\x00`), where Curl follows it and fails with `LabelLoop`. 8.22.0 also has no "no content" failure: an answer with no address is `DOH_OK`. Decide (ADR) what happens to `DnsAnswer.CanonicalNames`, `DnsMessageFailure.NoContent`/`LabelLoop`, `DohQueryResult`'s CNAME room and `DohTraceLines`' `CNAME:` lines, by measuring curl 8.22.0's `-v --trace-config doh` output for a CNAME answer. `DnsServerQuery` (the c-ares path) also reads the decoder's failures; keep its behaviour as measured for c-ares.
3. Each answer TTL is capped at `MAX_DNS_TTL`, 86400, before the minimum is taken (resp row 13, TTL `0x00ffff37` gives `(86400)`).

If this is too big for one run, split it: the TTL cap and NXDOMAIN code are small; the CNAME/NoContent change carries the trace and resolver ripple.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` has focused tests that `DnsAnswerDecoder.Decode` gives `NxDomain` for RCODE 3, `None` for unit1650's self-pointing CNAME row, and a TTL of 86400 for an answer TTL above 86400.
- [ ] The DoH resolver's exit code and `--trace-config doh` lines for an NXDOMAIN answer and a CNAME-only answer match curl 8.22.0 as measured, recorded in an ADR and pinned in `Curl.Networking.UnitTests`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
