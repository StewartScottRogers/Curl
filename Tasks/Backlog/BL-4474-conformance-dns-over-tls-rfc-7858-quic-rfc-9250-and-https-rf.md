---
id: BL-4474
title: Conformance: DNS over TLS (RFC 7858), QUIC (RFC 9250) and HTTPS (RFC 8484) for dns:// (DnsQuery part 2 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4462, BL-4473]
touches: [Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted, Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted, Documentation/Conformance/Extended/DnsQuery-Encrypted.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4474 — Conformance: DNS over TLS (RFC 7858), QUIC (RFC 9250) and HTTPS (RFC 8484) for dns:// (DnsQuery part 2 of 2)

## Goal

Curl's `dns://` encrypted transports (`--dns-transport tls`, `quic`, `https`) are measured against RFC 7858, RFC 9250
and RFC 8484 through the real curl command line, every client requirement is in a matrix mapped to a test, and every
failure is fixed or listed with its fix task.

## Context

Specifications: RFC 7858 (DNS over TLS: port 853, TCP framing over TLS, connection reuse rules), RFC 8310 where RFC
7858's profile rules bind a client, RFC 9250 (DNS over QUIC: ALPN `doq`, one query per bidirectional stream, Message
ID 0, STREAM FIN, error codes), RFC 8484 (DNS over HTTPS: GET with `dns=` base64url and POST with
`application/dns-message`, HTTP status handling). Builds on the reference DNS server BL-4473 wrote in
`Curl.Conformance.Extended.UnitLibrary/DnsQuery/`; add the transports in `DnsQuery/Encrypted/`. The QUIC side may use
`Curl.Conformance.Http3Server.UnitLibrary`'s or `Curl.Quic`'s server-side pieces; the HTTPS side may use the
harness's TLS route with an HTTP/1.1 or HTTP/2 responder.

Library: `Curl.Extended.Protocol.DnsQuery.UnitLibrary` (`Transport/Tls/DnsTlsTransport`,
`Transport/Quic/DnsQuicTransport` and `DnsQuicCertificateVerifier`, `Transport/Https/`; ADR-1140, ADR-1143,
ADR-1148).

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`DnsQuery:RFC7858-<section>-<n>` and the like), the matrix format and the `KnownFailures.txt` rule. Measure against
the specification, never against upstream curl. One file per case group; nothing added to `Harness/`. This task
touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed
here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix DnsQuery conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/DnsQuery-Encrypted.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of
      RFC 7858, RFC 9250 and RFC 8484 that binds a client an `DnsQuery:` ID mapped to a test or to `n/a: <reason>`;
      the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted/` run curl in process and assert wire bytes,
      stdout, stderr and exit code for at least: DoT on port 853 with the length prefix, a refused certificate (and
      `-k`), DoQ with ALPN `doq`, Message ID 0 and FIN, a DoQ server that sends a non-zero ID or a QUIC error code,
      DoH GET and POST bodies and media types, a DoH non-2xx status, and the default port of each transport.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix DnsQuery conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths, certificates or text).

## Notes

## Log

- 2026-11-02: Created.
