---
id: BL-4525
title: Conformance: CoAP RFC 7252 request/response and option cases for coap:// (Coap part 1b)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4524]
touches: [Curl.Conformance.Extended.UnitTests/Coap/RequestResponse, Documentation/Conformance/Extended/Coap.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4525 — Conformance: CoAP RFC 7252 request/response and option cases for coap:// (Coap part 1b)

## Goal

Curl's `coap://` client is measured against RFC 7252 section 5 (request/response, options) and section 3.1's option encoding.

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

Scope: section 5 (methods, token matching, options and their critical/elective, unsafe and repeatable handling, payload, Content-Format) and section 3.1 (option delta and length, extended 13 and 14 encodings). Uses the reference server BL-4524 adds; adds rows to `Documentation/Conformance/Extended/Coap.md`.

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Coap.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of RFC 7252 section 5 and section 3.1 that binds a client a `Coap:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Coap/RequestResponse/` run curl in process and assert datagram bytes, stdout, stderr and exit code for at least: each method (GET, `-d` POST, `-T` PUT, `-X DELETE`), token matching and a response with a mismatched token, an unrecognised critical option in a response, an unrecognised elective option ignored, and option delta and length extended encodings (13 and 14) in both directions.
- [ ] Any code this task adds under `Curl.Conformance.Extended.UnitLibrary/Coap/` (in its own new files) is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Coap conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: a7c897a9 6108a24d d503fe2c fb1c989b d8e3fb0e ef6c9026.
