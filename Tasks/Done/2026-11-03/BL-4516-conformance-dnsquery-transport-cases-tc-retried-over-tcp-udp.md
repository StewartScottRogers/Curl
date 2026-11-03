---
id: BL-4516
title: Conformance: DnsQuery transport cases - TC retried over TCP, UDP retries and give-up, matrix completion (BL-4473 part 3 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4514, BL-4515]
touches: [Curl.Conformance.Extended.UnitTests/DnsQuery/DnsQueryTransportTests.cs, Curl.Conformance.Extended.UnitLibrary/DnsQuery/Transport, Documentation/Conformance/Extended/DnsQuery.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-03
---
# BL-4516 — Conformance: DnsQuery transport cases - TC retried over TCP, UDP retries and give-up, matrix completion (BL-4473 part 3 of 3)

## Goal

Cases pin curl's `dns://` client's UDP and TCP transport rules, and `DnsQuery.md` maps every client requirement of RFC 1035, 3596, 2782, 8659, 3597, 5452, 7766 and 4501 to a test or `n/a`.

## Context

Split from BL-4473 (Deferred), which had all three parts as one task too big for one run. Specifications: RFC 1035 (4.1 message format, 4.1.4 compression, 4.2.1 UDP and the 512-octet limit, 4.2.2 TCP length prefix, 3.3 and 3.4 RDATA of A, NS, CNAME, PTR, MX, TXT, SOA), RFC 3596 (AAAA), RFC 2782 (SRV), RFC 8659 (CAA), RFC 3597 (unknown types, `\# len hex`), RFC 5452 (message ID and question matching), RFC 7766 (DNS over TCP: retry over TCP on TC, pipelining rules as they bind a client), RFC 4501 (the `dns:` URI: authority, name, `type=`, `class=`). BL-4474 covers DNS over TLS, QUIC and HTTPS.

Library under test: `Curl.Extended.Protocol.DnsQuery.UnitLibrary` (its `CLAUDE.md`: `Wire/`, `Rendering/`, `Client/`, `Transport/Udp/`, `Transport/Tcp/`; ADR-1124, ADR-1140, ADR-1143, ADR-1148). Options `--dns-transport` and `--dns-json`. With no server in the URL the client asks the system's first DNS server (`SystemDnsServers`); give every case an explicit server so no test reads the machine's configuration.

Follow `Documentation/Conformance/Extended/README.md` (harness: `ExtendedConformanceHarness.RunAsync` with `ReferenceRoute.Udp`/`ReferenceRoute.Tcp`, `[SpecRequirement]` IDs `DnsQuery:RFC1035-<section>-<n>` and the like, the matrix format, `KnownFailures.RunCaseAsync` and `KnownFailures.txt`). Measure against the specification, never against upstream curl. The reference server implements the wire format from the specification itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. A defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix DnsQuery conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed). This is the first extended protocol on the harness: no other protocol folder exists yet to copy from.

## Acceptance criteria

- [x] Cases in `Curl.Conformance.Extended.UnitTests/DnsQuery/DnsQueryTransportTests.cs` cover a UDP reply with TC set then the query retried over TCP with the 2-octet length prefix (RFC 1035 4.2.2, RFC 7766), and UDP retries and give-up driven on the injected clock (RFC 1035 4.2.1).
- [x] `Documentation/Conformance/Extended/DnsQuery.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of BL-4473's scope that binds a resolver client or stub a `DnsQuery:` ID mapped to a test or to `n/a: <reason>`, and links to `DnsQuery-Encrypted.md` (BL-4474).
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix DnsQuery conformance: ...` task; the known-failures check passes.
- [x] Every `[SpecRequirement]` the new cases carry is a row of `Documentation/Conformance/Extended/DnsQuery.md` and the harness's matrix check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] Code added under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- 2026-11-03 (BL-4669): Added the front matter's missing closing `---`; without it the board read no fields and took `touches` as `*`, so this task overlapped every other one. Narrowed `touches` from `Curl.Conformance.Extended.UnitLibrary/DnsQuery` to `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Transport`: any transport stand-in this task adds under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Transport/` (the reference server in `DnsQuery/Reference/` takes cases without edits, BL-4514), so it no longer covers `DnsQuery/Encrypted/` (BL-4474's).

- 2026-11-03 (run): Added `DnsQueryTransportTests` with four cases: TC on UDP then the same query asked over TCP after its two-octet prefix (RFC 1035 4.2.1, 4.2.2, RFC 7766 5), two silent attempts then an answer on the third with the same ID (RFC 1035 7.2), three silent attempts then exit 28 (RFC 1035 7.2), and the default type A, class IN over UDP (RFC 4501 2). All pass, so `KnownFailures.txt` gained no line. The retry cases run on a test-local clock that fires only a pending 5-second timer (the UDP attempt wait) once the server has received that attempt's query, so no other timer of the run can fire and no real time passes; chosen over the Coap `TimerJumpingClock`, which fires the earliest timer of any kind.
- Matrix completion: rows for the remaining client requirements of RFC 1035 4.2.1 (512 octets), RFC 5452 9.2 (random ID and port), RFC 7766 6.2.1, 6.2.1.1, 6.2.3 and 8 (pipelining, idle close, one write), RFC 4501 2 (no authority) are `n/a` with reasons: curl sends one query per connection, randomness is not observable in one run, and no case reads the machine's resolver. The link to `DnsQuery-Encrypted.md` is in the intro; BL-4474 writes that file.
- No code added under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/` (the reference server took the cases unchanged), so the coverage criterion holds with nothing to measure and Measure-CodeQuality.ps1 was not run. The tests use only `dns://localhost` and loopback reference routes: platform-neutral; CI on Linux and macOS is covered by that and the green fast run here.

## Log

- 2026-11-02: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 9b3ce63c e91ad7dd f3c72898 738dab01 d8e3fb0e 929e00c1 ef6c9026.
- 2026-11-03: Doing -> Done. DnsQuery transport cases pin TC retried over TCP, UDP retries and give-up on the injected clock; DnsQuery.md maps every client requirement
