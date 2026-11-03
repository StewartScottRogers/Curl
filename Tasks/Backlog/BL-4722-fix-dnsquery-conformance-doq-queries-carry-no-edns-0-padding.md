---
id: BL-4722
title: Fix DnsQuery conformance: DoQ queries carry no EDNS(0) padding (RFC 9250 5.4)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4719]
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Quic/DnsQuicTransport.cs, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/Quic, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted/DnsOverQuicCases.cs]
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

- 2026-11-03 (lane 2): The fix is written and green here but left uncommitted for the shift to stash: `DnsQuicTransport.AskAsync` sends `Framed(DnsEdnsPadding.Pad(query))` (doc comment updated); `DnsQuicTransportTests` pins the padded framing and adds `ExchangeAsync_Rfc9250Section5_4_PadsTheQueryWithTheEdnsPaddingOption`; the `DnsQuery:RFC9250-5.4-1` line is gone from `KnownFailures.txt`. DnsQuery.UnitTests 234/234 and Conformance.Extended.UnitTests 1717 passed, 1 skipped.
- Padding made `DnsOverQuicCases.Quic_DefaultPort853_SendsTheLengthPrefixedQueryWithMessageId0AndFinOnTheFirstStream` fail: it pinned the unpadded bytes. Its assertion now checks the length prefix, header (but ARCOUNT) and question instead, so `DnsOverQuicCases.cs` is added to touches. BL-4719 (Doing, a CI fix) holds the whole `Curl.Conformance.Extended.UnitTests`, so this task waits on it (lane rule 3). Left to the next claim: apply the stash, build, fast tests, Measure-CodeQuality.ps1 for Curl.Extended.Protocol.DnsQuery.UnitLibrary, commit, Done.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: af9ddcbf af7fc95c.
- 2026-11-03: Doing -> Backlog. Needs Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted/DnsOverQuicCases.cs, which BL-4719 (Doing) holds; fix is written and waits in the stash
