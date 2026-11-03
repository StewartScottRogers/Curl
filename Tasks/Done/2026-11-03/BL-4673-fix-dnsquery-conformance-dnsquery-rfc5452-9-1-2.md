---
id: BL-4673
title: Fix DnsQuery conformance: DnsQuery:RFC5452-9.1-2
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Udp/DnsUdpTransport.cs, Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/DnsUdpTransportTests.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4673 — Fix DnsQuery conformance: DnsQuery:RFC5452-9.1-2

## Goal

Curl's `dns://` client over UDP ignores a reply whose question is not its query's (RFC 5452 section 9.1) and keeps waiting for the matching reply.

## Context

Found by BL-4515's case `DnsQueryMessageTests.ReplyWithAnotherQuestion_IsIgnoredAndTheMatchingReplyAfterItTaken` in `Curl.Conformance.Extended.UnitTests/DnsQuery/DnsQueryMessageTests.cs`: the reference server sends a reply with the query's ID but the question `example.net. IN A`, then the right reply; curl writes the first. `DnsUdpTransport.IsAnswerTo` (`Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Udp/DnsUdpTransport.cs`) compares only the ID and QR bit; RFC 5452 section 9.1 says a resolver accepts a response only when its question section (QNAME, QTYPE, QCLASS) matches the query's, the QNAME compared without regard to ASCII case. The query's question starts at offset 12 of the query bytes it already holds.

## Acceptance criteria

- [x] `DnsQueryMessageTests.ReplyWithAnotherQuestion_IsIgnoredAndTheMatchingReplyAfterItTaken` passes and the `DnsQuery:RFC5452-9.1-2 BL-4673` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [x] A focused unit test in `Curl.Extended.Protocol.DnsQuery.UnitTests/Transport/DnsUdpTransportTests.cs` pins that a reply with another question is skipped and one whose QNAME differs only in case is taken (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] `Curl.Extended.Protocol.DnsQuery.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).

## Notes

- Done directly (a two-method change), not through the full /feature pipeline: `IsAnswerTo` now also requires the reply's first question to equal the query's (QNAME ASCII case-insensitive, QTYPE and QCLASS exact). Decided (ADR-1225): a reply with QDCOUNT 0 is still taken, as dig checks the question only when one is present.
- Measured: build clean, fast tests green (DnsQuery.UnitTests 220, Conformance.Extended 1531 passed), DnsQuery library 100% line and branch, worst CRAP 10. Change is platform-neutral (no paths, OS text or certificates), which covers CI on Linux and macOS.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 084392de 605f83e1 914c30cc 7a88b852.
- 2026-11-03: Doing -> Done. dns:// over UDP ignores a reply carrying another question and takes the matching one (RFC 5452 9.1)
