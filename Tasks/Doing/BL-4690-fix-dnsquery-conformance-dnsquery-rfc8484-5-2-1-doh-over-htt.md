---
id: BL-4690
title: Fix DnsQuery conformance: DnsQuery:RFC8484-5.2-1 (DoH over HTTP/2)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Https, Curl.Extended.Protocol.DnsQuery.UnitTests, Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted, Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed:
---
# BL-4690 — Fix DnsQuery conformance: DnsQuery:RFC8484-5.2-1 (DoH over HTTP/2)

## Goal

Under `--dns-transport https` curl sends its DoH request over HTTP/2 (ALPN `h2`), the minimum version RFC 8484 section 5.2 recommends, falling back to HTTP/1.1 only when the server does not select `h2`.

## Context

Found by BL-4681: `DnsOverHttpsCases.Https_Request_IsSentOverHttp2` (`DnsQuery:RFC8484-5.2-1`) fails: `Transport/Https/DohRequest` writes an HTTP/1.1 request. The fix needs ALPN on the TLS connection and an HTTP/2 framing of one request (the library may use the shared HTTP/2 layer if its references allow; otherwise write the few frames by hand). The conformance side needs an HTTP/2 DoH responder: `ReferenceRoute.Tls` offers no ALPN, so serve TLS from the server as `DnsTlsRefusalServer` does on a TCP route, or extend `DohReferenceServer` in `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted/` (keep its HTTP/1.1 cases passing and its code at 100% coverage). Record the decision in an ADR.

## Acceptance criteria

- [ ] The conformance case named in Context passes and its line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.DnsQuery.UnitTests` pins the fix (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the changed library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 1d7d20d2 4d2f7efb 4b59311c.
