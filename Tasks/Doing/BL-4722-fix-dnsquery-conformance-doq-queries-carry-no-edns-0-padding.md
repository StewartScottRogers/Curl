---
id: BL-4722
title: Fix DnsQuery conformance: DoQ queries carry no EDNS(0) padding (RFC 9250 5.4)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Quic/DnsQuicTransport.cs, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/Quic, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed:
---
# BL-4722 — Fix DnsQuery conformance: DoQ queries carry no EDNS(0) padding (RFC 9250 5.4)

## Goal

Curl pads every DNS over QUIC query with the EDNS(0) Padding option (RFC 7830, block-length policy of RFC 8467) as it already does for DoT, meeting RFC 9250 section 5.4.

## Context

Found by BL-4683: `DnsOverQuicCases.Quic_Query_IsPaddedWithTheEdnsPaddingOption` (`DnsQuery:RFC9250-5.4-1`) is listed in `KnownFailures.txt`; the query curl sends over DoQ has no OPT record. `DnsTlsTransport` calls `DnsEdnsPadding.Pad(query)`; `DnsQuicTransport.ExchangeAsync` sends the query unpadded. Pad it before framing (the Message ID is still zeroed by `Framed`).

## Acceptance criteria

- [ ] The DoQ query carries an OPT record whose first option is Padding (code 12); the case passes and its `KnownFailures.txt` line is removed.
- [ ] A focused unit test in `Curl.Extended.Protocol.DnsQuery.UnitTests` covers the padding; 100% line and branch coverage holds; build clean and fast tests green.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: af9ddcbf af7fc95c.
