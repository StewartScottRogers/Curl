---
id: BL-4515
title: Conformance: DnsQuery message cases - compression pointers, ID and question matching, RCODEs (BL-4473 part 2 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4514]
touches: [Curl.Conformance.Extended.UnitTests/DnsQuery/DnsQueryMessageTests.cs, Documentation/Conformance/Extended/DnsQuery.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4515 — Conformance: DnsQuery message cases - compression pointers, ID and question matching, RCODEs (BL-4473 part 2 of 3)

## Goal

Cases pin curl's `dns://` client against RFC 1035 message rules: compression pointers, reply ID and question matching, and each RCODE.

## Context

Split from BL-4473 (Deferred), which had all three parts as one task too big for one run. Specifications: RFC 1035 (4.1 message format, 4.1.4 compression, 4.2.1 UDP and the 512-octet limit, 4.2.2 TCP length prefix, 3.3 and 3.4 RDATA of A, NS, CNAME, PTR, MX, TXT, SOA), RFC 3596 (AAAA), RFC 2782 (SRV), RFC 8659 (CAA), RFC 3597 (unknown types, `\# len hex`), RFC 5452 (message ID and question matching), RFC 7766 (DNS over TCP: retry over TCP on TC, pipelining rules as they bind a client), RFC 4501 (the `dns:` URI: authority, name, `type=`, `class=`). BL-4474 covers DNS over TLS, QUIC and HTTPS.

Library under test: `Curl.Extended.Protocol.DnsQuery.UnitLibrary` (its `CLAUDE.md`: `Wire/`, `Rendering/`, `Client/`, `Transport/Udp/`, `Transport/Tcp/`; ADR-1124, ADR-1140, ADR-1143, ADR-1148). Options `--dns-transport` and `--dns-json`. With no server in the URL the client asks the system's first DNS server (`SystemDnsServers`); give every case an explicit server so no test reads the machine's configuration.

Follow `Documentation/Conformance/Extended/README.md` (harness: `ExtendedConformanceHarness.RunAsync` with `ReferenceRoute.Udp`/`ReferenceRoute.Tcp`, `[SpecRequirement]` IDs `DnsQuery:RFC1035-<section>-<n>` and the like, the matrix format, `KnownFailures.RunCaseAsync` and `KnownFailures.txt`). Measure against the specification, never against upstream curl. The reference server implements the wire format from the specification itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. A defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix DnsQuery conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed). This is the first extended protocol on the harness: no other protocol folder exists yet to copy from.

## Acceptance criteria

- [ ] Cases in `Curl.Conformance.Extended.UnitTests/DnsQuery/DnsQueryMessageTests.cs` run against BL-4514's reference server and assert stdout, stderr and exit code for: a reply using compression pointers (RFC 1035 4.1.4), a forward pointer refused and a looping pointer refused.
- [ ] Cases cover a reply with a wrong ID ignored and a reply with a wrong question ignored (RFC 5452), the right reply after it accepted.
- [ ] Cases cover each RCODE 0 to 5 (NOERROR, FORMERR, SERVFAIL, NXDOMAIN, NOTIMP, REFUSED) and its output and exit code.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix DnsQuery conformance: ...` task; the known-failures check passes.
- [ ] Every `[SpecRequirement]` the new cases carry is a row of `Documentation/Conformance/Extended/DnsQuery.md` and the harness's matrix check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [ ] Code added under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
