---
id: BL-4514
title: Conformance: DnsQuery reference DNS server over UDP and TCP, record-type and URL cases (BL-4473 part 1 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/DnsQuery/Reference, Curl.Conformance.Extended.UnitTests/DnsQuery/DnsQueryRecordTypeTests.cs, Curl.Conformance.Extended.UnitTests/DnsQuery/DnsQueryUrlTests.cs, Documentation/Conformance/Extended/DnsQuery.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-03
---
# BL-4514 — Conformance: DnsQuery reference DNS server over UDP and TCP, record-type and URL cases (BL-4473 part 1 of 3)

## Goal

A reference DNS server (UDP and TCP) under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/` answers curl's `dns://` client in process, and cases pin every listed record type and the RFC 4501 URL parts.

## Context

Split from BL-4473 (Deferred), which had all three parts as one task too big for one run. Specifications: RFC 1035 (4.1 message format, 4.1.4 compression, 4.2.1 UDP and the 512-octet limit, 4.2.2 TCP length prefix, 3.3 and 3.4 RDATA of A, NS, CNAME, PTR, MX, TXT, SOA), RFC 3596 (AAAA), RFC 2782 (SRV), RFC 8659 (CAA), RFC 3597 (unknown types, `\# len hex`), RFC 5452 (message ID and question matching), RFC 7766 (DNS over TCP: retry over TCP on TC, pipelining rules as they bind a client), RFC 4501 (the `dns:` URI: authority, name, `type=`, `class=`). BL-4474 covers DNS over TLS, QUIC and HTTPS.

Library under test: `Curl.Extended.Protocol.DnsQuery.UnitLibrary` (its `CLAUDE.md`: `Wire/`, `Rendering/`, `Client/`, `Transport/Udp/`, `Transport/Tcp/`; ADR-1124, ADR-1140, ADR-1143, ADR-1148). Options `--dns-transport` and `--dns-json`. With no server in the URL the client asks the system's first DNS server (`SystemDnsServers`); give every case an explicit server so no test reads the machine's configuration.

Follow `Documentation/Conformance/Extended/README.md` (harness: `ExtendedConformanceHarness.RunAsync` with `ReferenceRoute.Udp`/`ReferenceRoute.Tcp`, `[SpecRequirement]` IDs `DnsQuery:RFC1035-<section>-<n>` and the like, the matrix format, `KnownFailures.RunCaseAsync` and `KnownFailures.txt`). Measure against the specification, never against upstream curl. The reference server implements the wire format from the specification itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. A defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix DnsQuery conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed). This is the first extended protocol on the harness: no other protocol folder exists yet to copy from.

## Acceptance criteria

- [x] A reference DNS server in `Curl.Conformance.Extended.UnitLibrary/DnsQuery/` (UDP and TCP, its own RFC 1035 codec) lets BL-4515, BL-4516 and BL-4474 (encrypted transports in `DnsQuery/Encrypted/`) add cases without editing its files: a case supplies the answer records, and can override the reply header (ID, flags, RCODE, TC) and raw reply bytes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/DnsQuery/` run curl in process and assert query bytes, stdout (text and `--dns-json`), stderr and exit code for each of A, AAAA, NS, CNAME, PTR, MX, TXT, SOA, SRV, CAA and an unknown type (RFC 3597 `\# len hex`).
- [x] Cases cover `type=` and `class=` in the URL, and a server and port given in the URL authority (RFC 4501).
- [x] `Documentation/Conformance/Extended/DnsQuery.md` exists in the matrix format with the rows for these cases.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix DnsQuery conformance: ...` task; the known-failures check passes.
- [x] Every `[SpecRequirement]` the new cases carry is a row of `Documentation/Conformance/Extended/DnsQuery.md` and the harness's matrix check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] Code added under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- 2026-11-03 (BL-4669): Added the front matter's missing closing `---`; without it the board read no fields and took `touches` as `*`, so this task overlapped every other one. Narrowed `touches` from `Curl.Conformance.Extended.UnitLibrary/DnsQuery` to `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Reference`: Reference DNS server under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Reference/`, so it no longer covers `DnsQuery/Encrypted/` (BL-4474's).

- 2026-11-03 (lane 2): The reference DNS server is `DnsQuery/Reference/`: `DnsReferenceServer` (UDP `IDatagramReferenceServer` and TCP `IReferenceServer` with the two-octet prefix, answering every query on a connection), its own codec (`DnsReferenceQuery.Decode`, `DnsReferenceMessageWriter.WriteQuery`/`WriteReply`, `DnsReferenceName`), `DnsReferenceRecord` factories for every listed type, and `DnsReferenceReply` with `Id`, `Flags` (which carry TC and RCODE) and `Raw` overrides; a `DnsReferenceResponder` returns a list of replies so BL-4515 can send a wrong-ID reply before the right one and BL-4516 a TC reply then answer over TCP. `DnsReferenceRun.RunAsync` runs a command line against it over UDP or TCP. Choices (defaults, no ADR needed): the server compresses an owner name equal to the question's as a pointer to offset 12 and writes all other names in full; it never truncates on its own, so a case sets TC itself. The client's random message ID is read from the recorded query, so expected output uses it.
- Every case passes, so `KnownFailures.txt` is unchanged. Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary: 0 failing members. CI on Linux and macOS: the cases use no path, certificate or OS text, so they are platform-neutral; fast tests green here on Windows.

## Log

- 2026-11-02: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 0d2be213 5ee9557e f79f1697 2b051118 f3c72898 738dab01 d8e3fb0e 929e00c1 ef6c9026.
- 2026-11-03: Doing -> Done. Reference DNS server (UDP and TCP) and record-type and RFC 4501 URL cases run curl's dns:// client in process; DnsQuery.md matrix added
