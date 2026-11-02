---
id: BL-4470
title: Conformance: CoAP RFC 7252 messaging, options and request/response for coap:// (Coap part 1 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4462]
touches: [Curl.Conformance.Extended.UnitLibrary/Coap, Curl.Conformance.Extended.UnitTests/Coap, Documentation/Conformance/Extended/Coap.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4470 — Conformance: CoAP RFC 7252 messaging, options and request/response for coap:// (Coap part 1 of 3)

## Goal

Curl's `coap://` client is measured against RFC 7252's message format, messaging model, options, request/response
semantics and URI handling through the real curl command line over the harness's UDP route, every client
requirement is in a matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specification: RFC 7252 (The Constrained Application Protocol). Scope of this part: sections 3 (message format), 4
(messaging: confirmable, non-confirmable, ACK, Reset, retransmission with ACK_TIMEOUT, ACK_RANDOM_FACTOR and
MAX_RETRANSMIT, message ID and deduplication), 5 (request/response: piggybacked and separate responses, token
matching, options and their critical/elective handling, response codes, payload), 6 (`coap` URI, Uri-Host, Uri-Port,
Uri-Path, Uri-Query composition and decomposition) and 12 (registries the client uses). Part 2 (BL-4471) covers RFC
7959 and RFC 7641; part 3 (BL-4472) covers `coaps://` (DTLS, RFC 6347 and RFC 7252 section 9.1).

Library: `Curl.Extended.Protocol.Coap.UnitLibrary` (`CLAUDE.md`: `Wire/`, `Client/CoapExchange`, `CoapRequest`,
`CoapProtocolHandler`; ADR-1047). Retransmission runs on the injected `TimeProvider`; give the run a fake one.
Examples: `curl coap://host/path`, `-d`, `-T`, `-X DELETE`, `-f`, `-i`.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Coap:RFC7252-<section>-<n>`), the matrix format and the `KnownFailures.txt` rule. Measure against the
specification, never against upstream curl. The reference server implements the wire format from the specification
itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`.
This task touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is
fixed here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix Coap conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Coap.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of this scope
      that binds a client an `Coap:` ID and maps it to a test or to `n/a: <reason>`, links to
      `Coap-BlockwiseObserve.md` (BL-4471) and `Coap-Dtls.md` (BL-4472), and the harness's matrix check passes.
- [ ] A reference CoAP server in `Curl.Conformance.Extended.UnitLibrary/Coap/` lets BL-4471 and BL-4472 add behaviour
      in `Coap/BlockwiseObserve/` and `Coap/Dtls/` without editing this task's files.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Coap/` run curl in process and assert datagram bytes, stdout,
      stderr and exit code for at least: each method, piggybacked and separate responses, a duplicate response, a
      Reset (exit 56), retransmission timing and give-up (exit 28), token mismatch, an unrecognised critical option
      in a response, option delta and length extended encodings, the Uri-* options built from host, port, path
      segments and query (percent-encoding included), 4.xx and 5.xx with and without `-f`, and `-i`'s status line.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Coap conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/Coap/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
