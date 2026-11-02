---
id: BL-4473
title: Conformance: DNS RFC 1035 messages over UDP and TCP and the RFC 4501 URL for dns:// (DnsQuery part 1 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4462]
touches: [Curl.Conformance.Extended.UnitLibrary/DnsQuery, Curl.Conformance.Extended.UnitTests/DnsQuery, Documentation/Conformance/Extended/DnsQuery.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4473 — Conformance: DNS RFC 1035 messages over UDP and TCP and the RFC 4501 URL for dns:// (DnsQuery part 1 of 2)

## Goal

Curl's `dns://` client is measured against RFC 1035 messages over UDP and TCP and the RFC 4501 URL through the real
curl command line, every client requirement is in a matrix mapped to a test, and every failure is fixed or listed
with its fix task.

## Context

Specifications: RFC 1035 (4.1 message format, 4.1.4 compression, 4.2.1 UDP and the 512-octet limit, 4.2.2 TCP
length prefix, 3.3 and 3.4 RDATA of A, NS, CNAME, PTR, MX, TXT, SOA), RFC 3596 (AAAA), RFC 2782 (SRV), RFC 8659
(CAA), RFC 3597 (unknown types, `\# len hex`), RFC 5452 (message ID and question matching), RFC 7766 (DNS over TCP:
retry over TCP on TC, pipelining rules as they bind a client), RFC 4501 (the `dns:` URI: authority, name, `type=`,
`class=`). Part 2 (BL-4474) covers DNS over TLS, QUIC and HTTPS.

Library: `Curl.Extended.Protocol.DnsQuery.UnitLibrary` (`CLAUDE.md`: `Wire/`, `Rendering/`, `Client/`,
`Transport/Udp/`, `Transport/Tcp/`; ADR-1124, ADR-1140, ADR-1143, ADR-1148). Options `--dns-transport` and
`--dns-json`. With no server in the URL the client asks the system's first DNS server (`SystemDnsServers`); give
every case an explicit server so no test reads the machine's configuration.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`DnsQuery:RFC1035-<section>-<n>` and the like), the matrix format and the `KnownFailures.txt` rule. Measure against
the specification, never against upstream curl. The reference server implements the wire format from the
specification itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to
`Harness/`. This task touches only its own conformance files so protocols run in parallel: a fault in its own server
or cases is fixed here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix DnsQuery conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/DnsQuery.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of this
      scope that binds a resolver client or stub an `DnsQuery:` ID mapped to a test or to `n/a: <reason>`, links to
      `DnsQuery-Encrypted.md` (BL-4474), and the harness's matrix check passes.
- [ ] A reference DNS server in `Curl.Conformance.Extended.UnitLibrary/DnsQuery/` (UDP and TCP) lets BL-4474 add the
      encrypted transports in `DnsQuery/Encrypted/` without editing this task's files.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/DnsQuery/` run curl in process and assert query bytes, stdout
      (text and `--dns-json`), stderr and exit code for at least: each record type listed above and an unknown one,
      compression pointers (forward and looping pointers refused), a reply with a wrong ID or question ignored, TC
      set then retried over TCP, UDP retries and give-up on the injected clock, each RCODE, `type=` and `class=` in the
      URL, and a server and port given in the URL.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix DnsQuery conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
