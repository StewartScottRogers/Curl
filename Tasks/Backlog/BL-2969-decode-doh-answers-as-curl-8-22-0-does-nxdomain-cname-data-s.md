---
id: BL-2969
title: Decode DoH answers as curl 8.22.0 does: NXDOMAIN, CNAME data skipped, TTL capped at 86400
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2970]
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

- 2026-10-21 lane 1: requeued behind BL-2970 after landing the small half (commit 8677cc087): `DnsAnswerDecoder` returns `NxDomain` ("No such name") for RCODE 3 (`Decode_AnNxdomainRcode_FailsWithNxDomain`) and caps each answer TTL at 86400 (`Decode_AnAnswerTtlAboveOneDay_IsCappedAtOneDay`), ADR-0738; build clean, 44 fast test projects green, `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 0 failing members. What is left - the self-pointing CNAME row decoding `None`, and the measured 8.22.0 trace lines for NXDOMAIN and CNAME-only answers - is BL-2970's work; once it is Done, this task's next claim only checks these criteria and finishes. The board refused to move this task to Done with criteria narrowed to the split, so the criteria stay as claimed.

- Split (as the Context allowed) after reading curl 8.22.0's `lib/vdns/doh.c`: besides the three decoder changes, 8.22.0 rewrote the A/AAAA `--trace-config doh` lines (`[DoH] [A] error decoding response: ...`, `DoH request complete, N to go`, no `hostname:`/`TTL:`/`CNAME:` entry). Skipping CNAME data and dropping `NoContent` changes that entry, so they go together with the new line form to BL-2970, which measures them with the local curl 8.22.0. This task did NXDOMAIN and the TTL cap.
- Choice: `NxDomain` is appended at the end of `DnsMessageFailure`, so `DnsMessageFailureText`'s table stays indexed by value. Until BL-2970 the trace keeps 8.21.0's form with the new text: `[DNS] DoH: No such name type A for <host>` (ADR-0738).
- `DnsServerQuery` (c-ares) reads RCODE itself before decoding, so it is unchanged.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: bd93dc30 0841b2d7 2a446683.
- 2026-10-21: Doing -> Backlog. Waits on BL-2970: the CNAME row and curl 8.22.0's measured trace lines; NXDOMAIN and the TTL cap landed in 8677cc087
