---
id: BL-4690
title: Fix DnsQuery conformance: DnsQuery:RFC8484-5.2-1 (DoH over HTTP/2)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.DnsQuery.UnitLibrary/Transport/Https, Curl.Extended.Protocol.DnsQuery.UnitLibrary/Curl.Extended.Protocol.DnsQuery.UnitLibrary.csproj, Curl.Extended.Protocol.DnsQuery.UnitLibrary/CLAUDE.md, Curl.Extended.Protocol.DnsQuery.UnitTests, Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted, Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4690 — Fix DnsQuery conformance: DnsQuery:RFC8484-5.2-1 (DoH over HTTP/2)

## Goal

Under `--dns-transport https` curl sends its DoH request over HTTP/2 (ALPN `h2`), the minimum version RFC 8484 section 5.2 recommends, falling back to HTTP/1.1 only when the server does not select `h2`.

## Context

Found by BL-4681: `DnsOverHttpsCases.Https_Request_IsSentOverHttp2` (`DnsQuery:RFC8484-5.2-1`) fails: `Transport/Https/DohRequest` writes an HTTP/1.1 request. The fix needs ALPN on the TLS connection and an HTTP/2 framing of one request (the library may use the shared HTTP/2 layer if its references allow; otherwise write the few frames by hand). The conformance side needs an HTTP/2 DoH responder: `ReferenceRoute.Tls` offers no ALPN, so serve TLS from the server as `DnsTlsRefusalServer` does on a TCP route, or extend `DohReferenceServer` in `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted/` (keep its HTTP/1.1 cases passing and its code at 100% coverage). Record the decision in an ADR.

## Acceptance criteria

- [x] The conformance case named in Context passes and its line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.DnsQuery.UnitTests` pins the fix (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the changed library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Added the library csproj (reference to the shared layer Curl.Http2.UnitLibrary) and its CLAUDE.md to touches: the HTTP/2 framing comes from that layer, as in the gRPC extension. No task in Doing on origin/work/dark-factory names either.
- Plan and decision (ADR-1231): the DoH handshake offers ALPN h2 then http/1.1; under h2 `DohHttp2Exchange` sends the preface and one POST stream through `Http2Connection` over `DohConnectionStream`, otherwise the HTTP/1.1 path is unchanged. Conformance: `DohHttp2ReferenceServer` serves TLS with ALPN h2 on a TCP route and HTTP/2 through `GrpcReferenceServer`; the case now asserts the request the HTTP/2 server received (a TCP route records only ciphertext, so the old ClientBytes preface check could not see the plaintext).
- Measured: Measure-CodeQuality for both changed libraries: 100% line and branch, 0 failing members. Full build -warnaserror clean, 115 fast test projects green. Tests are platform-neutral (in-memory, self-signed cert under -k, as the existing DoH cases), which covers CI on Linux and macOS.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 1d7d20d2 4d2f7efb 4b59311c.
- 2026-11-03: Doing -> Done. DoH offers ALPN h2 and sends its query over HTTP/2 when selected, falling back to HTTP/1.1; DnsQuery:RFC8484-5.2-1 passes
