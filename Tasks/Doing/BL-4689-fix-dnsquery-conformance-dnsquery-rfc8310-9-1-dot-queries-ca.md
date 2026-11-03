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
completed:
---
# BL-4689 — Fix DnsQuery conformance: DnsQuery:RFC8310-9-1 (DoT queries carry EDNS(0) Padding)

## Goal

Under `--dns-transport tls` curl pads its query with the EDNS(0) Padding option (RFC 7830), using RFC 8467's block-length policy (pad queries to a multiple of 128 octets), as RFC 8310 section 9 recommends.

## Context

Found by BL-4681: `DnsOverTlsCases.Tls_Query_IsPaddedWithTheEdnsPaddingOption` (`DnsQuery:RFC8310-9-1`) fails: the DoT query carries no OPT record. The case expects ARCOUNT 1 and an OPT record whose first option is Padding (code 12). Add the OPT record and option in the query encoder under `Wire/` and ask for it from `Transport/Tls/DnsTlsTransport` (consider DoH and DoQ too, RFC 8467 section 4.1). Record the policy in an ADR.

## Acceptance criteria

- [ ] The conformance case named in Context passes and its line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.DnsQuery.UnitTests` pins the fix (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the changed library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 1d7d20d2 4d2f7efb 4b59311c.
