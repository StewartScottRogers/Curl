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
completed: 2026-11-03
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

- [x] `Documentation/Conformance/Extended/DnsQuery-Encrypted.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of
      RFC 9250 that binds a client a `DnsQuery:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check
      passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/DnsQuery/Encrypted/` run curl in process and assert wire bytes,
      stdout, stderr and exit code for at least: DoQ with ALPN `doq`, Message ID 0 and FIN, a DoQ server that sends a
      non-zero ID or a QUIC error code, and DoQ's default port.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix DnsQuery conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/DnsQuery/Encrypted/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths, certificates or text).

## Notes

- Filed by BL-4474's split (2026-11-03).
- 2026-11-03 (lane 2): DoqReferenceServer (+ DoqReceivedQuery) in DnsQuery/Encrypted/ answers on the query's stream with FIN, or closes with a scripted code; a non-zero Message ID is scripted through DnsReferenceReply.Id. Six cases in DnsOverQuicCases plus DoqReferenceServerTests (a query split across datagrams, the branch curl's one-datagram query never reaches). 16 RFC 9250 rows in DnsQuery-Encrypted.md (10 tested, 6 n/a).
- Measured: ALPN doq, port 853, length prefix, stream 0, FIN, Message ID 0, curl's DOQ_NO_ERROR close, exit 8 plus DOQ_PROTOCOL_ERROR on a non-zero ID, exit 56 on a server close, exit 60 on an untrusted certificate all pass. Two fail and are listed in KnownFailures.txt: no EDNS(0) padding on DoQ queries (BL-4719) and DoQ to UDP port 53 not refused (BL-4720, depends on BL-4719 because both change DnsQuicTransport.cs).
- Decision (sensible default): the padding case checks EDNS(0) Padding, as the DoT case does, because RFC 9250 5.4 allows it and curl already pads DoT; the port-53 case asserts only a non-zero exit and no handshake, leaving the exit code to BL-4720. The server-close case pins only the message prefix, since the rest is Curl.Quic's IOException text.
- Quality: Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary reported 0 failing members. All tests are platform-neutral (no paths, a generated in-memory certificate, Environment.NewLine in the stderr comparison); the CI-on-Linux/macOS criterion is met by that and fast tests green here.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 21a4291a 19f3f6e9 f3c72898 d8e3fb0e 627fbee3 17248609.
- 2026-11-03: Doing -> Done. DoQ measured against RFC 9250: matrix rows, six cases, two known failures filed as BL-4719 and BL-4720
