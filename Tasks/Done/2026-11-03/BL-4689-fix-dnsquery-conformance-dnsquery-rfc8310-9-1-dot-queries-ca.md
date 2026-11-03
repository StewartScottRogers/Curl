---
id: BL-4689
title: Fix DnsQuery conformance: DnsQuery:RFC8310-9-1 (DoT queries carry EDNS(0) Padding)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Tls, Curl.Extended.Protocol.DnsQuery.UnitLibrary/Wire, Curl.Extended.Protocol.DnsQuery.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4689 — Fix DnsQuery conformance: DnsQuery:RFC8310-9-1 (DoT queries carry EDNS(0) Padding)

## Goal

Under `--dns-transport tls` curl pads its query with the EDNS(0) Padding option (RFC 7830), using RFC 8467's block-length policy (pad queries to a multiple of 128 octets), as RFC 8310 section 9 recommends.

## Context

Found by BL-4681: `DnsOverTlsCases.Tls_Query_IsPaddedWithTheEdnsPaddingOption` (`DnsQuery:RFC8310-9-1`) fails: the DoT query carries no OPT record. The case expects ARCOUNT 1 and an OPT record whose first option is Padding (code 12). Add the OPT record and option in the query encoder under `Wire/` and ask for it from `Transport/Tls/DnsTlsTransport` (consider DoH and DoQ too, RFC 8467 section 4.1). Record the policy in an ADR.

## Acceptance criteria

- [x] The conformance case named in Context passes and its line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.DnsQuery.UnitTests` pins the fix (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the changed library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Plan: `Wire/DnsEdnsPadding.Pad` appends an OPT record (root, TYPE 41, CLASS 1232, TTL 0) with one Padding option (code 12) whose zero octets bring the query to a multiple of 128 octets, and adds one to ARCOUNT; `Transport/Tls/DnsTlsTransport` pads the query just before framing it. The handler and other transports are unchanged, so the change stays inside this task's `touches`. Policy recorded in ADR-1230.
- Choices: CLASS (UDP payload size) 1232, DNS Flag Day 2020's value, since it means nothing over TLS but must be set to something; a query already on a 128-octet boundary still carries an empty Padding option, so every DoT query has the same shape.
- DoH and DoQ padding (RFC 8467 section 4.1) are filed as BL-4718; they live in `Transport/Https` and `Transport/Quic`, outside this task's `touches`.
- Unit tests: `DnsEdnsPaddingTests` (three cases) and `DnsTlsTransportTests.ExchangeAsync_Rfc8310Section9_PadsTheQueryWithTheEdnsPaddingOption` pin the fix (ADR-0482). The conformance case passes with its KnownFailures line removed.
- Verified: build `-warnaserror` clean; the fast tests are green (DnsQuery.UnitTests 226, Conformance.Extended.UnitTests 1698 passed); `Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.DnsQuery.UnitLibrary` reports 0 failing members. The change is pure byte arithmetic, so it does not depend on the platform.
- `dotnet format --verify-no-changes` flags import ordering in `Client/DnsQueryExtension.cs`, which this change does not touch and which is outside `touches`; left as it is.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 1d7d20d2 4d2f7efb 4b59311c.
- 2026-11-03: Doing -> Done. DoT queries carry EDNS(0) Padding to a 128-octet block (RFC 8310 section 9, RFC 8467); DnsQuery:RFC8310-9-1 passes
