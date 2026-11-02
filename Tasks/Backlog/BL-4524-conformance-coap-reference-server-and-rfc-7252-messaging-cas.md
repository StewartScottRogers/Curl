---
id: BL-4524
title: Conformance: CoAP reference server and RFC 7252 messaging cases for coap:// (Coap part 1a)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/Coap, Curl.Conformance.Extended.UnitTests/Coap/Messaging, Documentation/Conformance/Extended/Coap.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4524 — Conformance: CoAP reference server and RFC 7252 messaging cases for coap:// (Coap part 1a)

## Goal

A reference CoAP server written from RFC 7252 runs under the extended harness and Curl's `coap://` client is measured against RFC 7252 sections 3 and 4 (message format and messaging).

## Context

Specification: RFC 7252 (The Constrained Application Protocol). Split from BL-4470 (Coap part 1 of 3) for size; BL-4471 (RFC 7959, RFC 7641) and BL-4472 (`coaps://`) build on the reference server this chain adds.

Library under test: `Curl.Extended.Protocol.Coap.UnitLibrary` (`CLAUDE.md`: `Wire/`, `Client/CoapExchange`, `CoapRequest`,
`CoapProtocolHandler`; ADR-1047). Retransmission runs on the injected `TimeProvider`; give the run a fake one
(`ExtendedConformanceHarness.RunAsync(..., timeProvider)`, see `ExtendedCurlRunTimeoutTests`). Routes: `ReferenceRoute.Udp`
with an `IDatagramReferenceServer` (`Curl.Conformance.Extended.UnitLibrary/Harness/`).

Follow `Documentation/Conformance/Extended/README.md` and ADR-1186/ADR-1188: the harness, `[SpecRequirement]` IDs
(`Coap:RFC7252-<section>-<n>`), the matrix format and the `KnownFailures.txt` rule. Measure against the specification,
never against upstream curl. The reference server implements the wire format from the specification itself and
references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. A fault in this
chain's own server or cases is fixed here, but a defect in the extension library is listed in `KnownFailures.txt` with a
filed High task `Fix Coap conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

Scope: section 3 (message format: version, type, token length, code, message ID, token, payload marker) and section 4 (confirmable, non-confirmable, ACK, Reset, retransmission with ACK_TIMEOUT, ACK_RANDOM_FACTOR and MAX_RETRANSMIT, message ID and deduplication). This part creates `Documentation/Conformance/Extended/Coap.md`; BL-4525 and BL-4526 add rows to it.

## Acceptance criteria

- [ ] A reference CoAP server in `Curl.Conformance.Extended.UnitLibrary/Coap/` (its own encoder and decoder, configurable responses: piggybacked, separate, duplicate, Reset, silence, arbitrary options) lets BL-4525, BL-4526, BL-4471 and BL-4472 add behaviour in their own files and subfolders (`Coap/BlockwiseObserve/`, `Coap/Dtls/`) without editing this task's files.
- [ ] `Documentation/Conformance/Extended/Coap.md` exists in the README's matrix format and gives every MUST, MUST NOT, SHOULD and SHOULD NOT of RFC 7252 sections 3 and 4 that binds a client a `Coap:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Coap/Messaging/` run curl in process and assert datagram bytes, stdout, stderr and exit code for at least: a confirmable GET with a piggybacked response, a separate response (empty ACK then CON response, which curl ACKs), a duplicate response, a Reset (exit 56), retransmission timing (ACK_TIMEOUT doubling within ACK_RANDOM_FACTOR) and give-up after MAX_RETRANSMIT (exit 28), and the header fields of every datagram curl sends.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Coap/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Coap conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
