---
id: BL-4688
title: Fix DnsQuery conformance: DnsQuery:RFC8484-4.1-2 (DoH queries use DNS ID 0)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport, Curl.Extended.Protocol.DnsQuery.UnitLibrary/Client/DnsQueryProtocolHandler.cs, Curl.Extended.Protocol.DnsQuery.UnitLibrary/Client/IDnsQueryTransport.cs, Curl.Extended.Protocol.DnsQuery.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4688 — Fix DnsQuery conformance: DnsQuery:RFC8484-4.1-2 (DoH queries use DNS ID 0)

## Goal

Under `--dns-transport https` curl sends every DNS query with message ID 0, as RFC 8484 section 4.1 says a DoH client using `application/dns-message` SHOULD for cache friendliness.

## Context

Found by BL-4681: `DnsOverHttpsCases.Https_Query_CarriesDnsId0` (`DnsQuery:RFC8484-4.1-2`) fails because `DnsQueryProtocolHandler.TransferAsync` draws a random ID for every transport. Let the transport choose the ID (DoH: 0; UDP, TCP and DoT keep the random ID, RFC 5452), and keep the answer's ID check.

## Acceptance criteria

- [x] The conformance case named in Context passes and its line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.DnsQuery.UnitTests` pins the fix (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the changed library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Touches widened with `Client/IDnsQueryTransport.cs`: the transport tells the handler which ID to use through a new default interface member, `QueriesWithMessageIdZero` (false by default, true for `DnsHttpsTransport`). No task in Doing on `origin/work/dark-factory` names that file.
- First tried rewriting the ID inside the DoH transport, as DoQ does. Dropped it: `Https_DefaultPort443_PostsTheQueryAsAnApplicationDnsMessageBody` expects the written header to show the wire ID (`id: 0`). The handler now encodes ID 0 itself and keeps the answer's ID check. Recorded in ADR-1229.
- Moved the ID choice and the answer-ID check out of `TransferAsync` (`MessageIdFor`, `RequireQueryId`) to keep its complexity under 10. `Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.DnsQuery.UnitLibrary`: 100% line and branch coverage, 0 failing members, worst CRAP 10.
- Tests: `DnsQueryProtocolHandlerTests.ExecuteAsync_DnsTransportHttps_Rfc8484Section4_1_QueriesWithDnsId0AndAcceptsTheAnswersId0` and `DnsHttpsTransportTests.QueriesWithMessageIdZero_Rfc8484Section4_1_IsTrue`. DnsQuery.UnitTests 222 passed. Conformance.Extended.UnitTests 1698 passed, 1 skipped. Full solution build with -warnaserror is clean and the fast tests are green. The change is platform-neutral: no paths, OS errors or certificates.
## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 1d7d20d2 4d2f7efb 4b59311c.
- 2026-11-03: Doing -> Done. DoH queries carry DNS ID 0 (RFC 8484 section 4.1); DnsQuery:RFC8484-4.1-2 passes
