---
id: BL-4472
title: Conformance: DTLS 1.2 (RFC 6347) and RFC 7252 section 9.1 for coaps:// (Coap part 3 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4524]
touches: [Curl.Conformance.Extended.UnitLibrary/Coap/Dtls, Curl.Conformance.Extended.UnitTests/Coap/Dtls, Documentation/Conformance/Extended/Coap-Dtls.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
model: opus
requirement: none
created: 2026-11-02
completed:
---
# BL-4472 — Conformance: DTLS 1.2 (RFC 6347) and RFC 7252 section 9.1 for coaps:// (Coap part 3 of 3)

## Goal

Curl's `coaps://` client is measured against DTLS 1.2 (RFC 6347) and RFC 7252 section 9.1 through the real curl
command line over the harness's UDP route, every client requirement is in a matrix mapped to a test, and every
failure is fixed or listed with its fix task.

## Context

Specifications: RFC 6347 (DTLS 1.2: record layer and epochs, sequence numbers and the replay window (4.1.2.6),
HelloVerifyRequest cookie exchange (4.2.1), handshake message fragmentation and reassembly (4.2.3), flight
retransmission timers (4.2.4), alerts) with the TLS 1.2 handshake it inherits (RFC 5246, the cipher suites the
library offers) and RFC 7252 section 9.1 (`coaps` default port 5684, certificate mode, one CoAP message per record).
Builds on the reference server BL-4470 wrote in `Curl.Conformance.Extended.UnitLibrary/Coap/`; the DTLS server side
goes in `Coap/Dtls/`. It must be written from the RFCs, not from the client's `Dtls/` code; `Curl.Tls` may be used
for the PRF and key block, as the library does (ADR-1042).

Library: `Curl.Extended.Protocol.Coap.UnitLibrary` (`Dtls/Handshake/`, `Dtls/Record/`, `Dtls/Retransmission/`,
`Dtls/Wire/`, `Secure/DtlsDatagramChannel`; ADR-1047). Retransmission runs on the injected `TimeProvider`.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Coap:RFC6347-<section>-<n>`, `Coap:RFC7252-9.1-<n>`), the matrix format and the `KnownFailures.txt` rule.
Measure against the specification, never against upstream curl. One file per case group; nothing added to
`Harness/`. This task touches only its own conformance files so protocols run in parallel: a fault in its own server
or cases is fixed here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix Coap conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Coap-Dtls.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of RFC 6347
      (and the RFC 5246 handshake rules it inherits) and RFC 7252 section 9.1 that binds a client an `Coap:` ID mapped
      to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Coap/Dtls/` run curl in process for `coaps://` and assert datagram
      bytes, stdout, stderr and exit code for at least: a full handshake with a HelloVerifyRequest cookie, a server
      flight fragmented and reordered, a lost flight retransmitted on the timer, a replayed and an old-epoch record
      dropped, a fatal alert, a certificate the TLS provider refuses (and `-k`), and a CoAP exchange over the
      protected records.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Coap conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Coap/Dtls/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths, certificates or text).

## Notes

## Log

- 2026-11-02: Created.
