---
id: BL-4684
title: Fix DnsQuery conformance: DnsQuery:RFC8484-4.1-2 (DoH queries use DNS ID 0)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport, Curl.Extended.Protocol.DnsQuery.UnitLibrary/Client/DnsQueryProtocolHandler.cs, Curl.Extended.Protocol.DnsQuery.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed:
---
# BL-4684 — Fix DnsQuery conformance: DnsQuery:RFC8484-4.1-2 (DoH queries use DNS ID 0)

## Goal

Under `--dns-transport https` curl sends every DNS query with message ID 0, as RFC 8484 section 4.1 says a DoH client using `application/dns-message` SHOULD for cache friendliness.

## Context

Found by BL-4681: `DnsOverHttpsCases.Https_Query_CarriesDnsId0` (`DnsQuery:RFC8484-4.1-2`) fails because `DnsQueryProtocolHandler.TransferAsync` draws a random ID for every transport. Let the transport choose the ID (DoH: 0; UDP, TCP and DoT keep the random ID, RFC 5452), and keep the answer's ID check.

## Acceptance criteria

- [ ] The conformance case named in Context passes and its line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.DnsQuery.UnitTests` pins the fix (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the changed library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-03: Created.
