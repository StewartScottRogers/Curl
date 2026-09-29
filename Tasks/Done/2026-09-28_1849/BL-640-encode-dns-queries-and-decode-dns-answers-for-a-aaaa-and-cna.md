---
id: BL-640
title: Encode DNS queries and decode DNS answers for A, AAAA and CNAME records
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-639]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-640 — Encode DNS queries and decode DNS answers for A, AAAA and CNAME records

## Goal

A DNS message codec in `Curl.Networking.UnitLibrary` encodes the query curl 8.21.0 sends for DoH (ID 0, RD set, one question, A or AAAA) byte for byte, and decodes answers (name compression, CNAME chains, TTLs, RCODE errors, truncated or malformed messages) into addresses or a typed failure.

## Context

- Conformance audit 2026-09-28, row 27. Design: BL-639's ADR (which records the query bytes curl sent).
- RFC 1035 (message format and compression), RFC 3596 (AAAA), RFC 8484 section 4.1 (ID 0). Pure code: bytes in, bytes out.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` reproduce the query bytes BL-639 recorded, decode answers with compression pointers, a CNAME chain, a pointer loop (rejected), NXDOMAIN and SERVFAIL, and a truncated message.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built in-session rather than through the full `/feature` agent chain: ADR-0152 is the plan, and the codec is a port of curl 8.21.0's `doh_req_encode` and `doh_resp_decode` (`lib/doh.c`), so every rule mirrors curl's, including its failure order and codes.
- New types: `DnsRecordType`, `DnsQueryEncoder` (-> `DnsQueryEncoding`), `DnsAnswerDecoder` (-> `DnsAnswer`), `DnsMessageFailure` and `DnsMessageFailureText` (curl's `doh_strerror` texts, for BL-641's `--trace-config doh` lines).
- Choices taken from curl's source, no new ADR needed: at most 24 addresses and 4 CNAMEs kept (`DOH_MAX_ADDR`, `DOH_MAX_CNAME`); a CNAME name gives up after 128 labels/pointers (`LabelLoop`); DNAME answers accepted and skipped; the TTL is the smallest across answer records, `int.MaxValue` with none; label bytes are read as Latin-1 so no byte is lost; host names are encoded as UTF-8 (they reach the resolver already in ASCII form).
- The CNAMEs are decoded but not re-queried, as in curl: the addresses returned are those of the asked type anywhere in the answer section.
- Tests: `DnsQueryEncoderTests` (11), `DnsAnswerDecoderTests` (27 incl. rows), `DnsMessageFailureTextTests` (14). `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. DnsQueryEncoder writes curl's measured DoH query and DnsAnswerDecoder decodes A/AAAA/CNAME answers with compression, loops, RCODE and truncation failures as curl does
