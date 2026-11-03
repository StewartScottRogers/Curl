---
id: BL-4683
title: Conformance: DNS over QUIC (RFC 9250) for dns:// (DnsQuery encrypted part 3 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4681, BL-4682]
touches: [Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted, Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted, Documentation/Conformance/Extended/DnsQuery-Encrypted.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-03
completed:
---
# BL-4683 — Conformance: DNS over QUIC (RFC 9250) for dns:// (DnsQuery encrypted part 3 of 3)

## Goal

Curl's `dns://` DNS over QUIC transport (`--dns-transport quic`) is measured against RFC 9250 through the real curl
command line, every client requirement is in BL-4681's matrix mapped to a test, and every failure is fixed or listed
with its fix task; this finishes BL-4474's work.

## Context

Split from BL-4474 (part 3 of 3; read its Context in `Tasks/Deferred/` for the harness, ID, matrix and
`KnownFailures.txt` rules). Builds on BL-4681 (`DnsQuery-Encrypted.md` and the `DnsQuery/Encrypted/` folder) and
BL-4682 (the conformance HTTP/3 server negotiating `doq`). Write a DoQ reference server in `DnsQuery/Encrypted/` as
an `IDatagramReferenceServer` over `ReferenceRoute.Udp(853, ...)` driving `Curl.Conformance.Http3Server`'s
`Http3ServerConnection` with the application protocol `doq`, the way
`Curl.Conformance.Extended.UnitLibrary/WebTransport/Server/WebTransportReferenceServer.cs` drives it for h3 (see
`Curl.Conformance.Extended.UnitTests/WebTransport/Cases/WebTransportRun.cs` and `WebTransportTestClient` for the
certificate and settings): accept each client bidirectional stream, read the length-prefixed query to FIN, record it,
and answer on the same stream with FIN (`Write(streamId, bytes, endStream: true)`), or close the connection with a
scripted application error code (`Close(code)`), or answer with a scripted non-zero Message ID. Library:
`Curl.Extended.Protocol.DnsQuery.UnitLibrary` `Transport/Quic/DnsQuicTransport` and `DnsQuicCertificateVerifier`
(ADR-1143).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/DnsQuery-Encrypted.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of
      RFC 9250 that binds a client a `DnsQuery:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check
      passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted/` run curl in process and assert wire bytes,
      stdout, stderr and exit code for at least: DoQ with ALPN `doq`, Message ID 0 and FIN, a DoQ server that sends a
      non-zero ID or a QUIC error code, and DoQ's default port.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix DnsQuery conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths, certificates or text).

## Notes

- Filed by BL-4474's split (2026-11-03).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 21a4291a 19f3f6e9 f3c72898 d8e3fb0e 627fbee3 17248609.
