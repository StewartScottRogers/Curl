---
id: BL-4526
title: Conformance: CoAP RFC 7252 URI, response code, -f and -i cases and the complete Coap matrix (Coap part 1c)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4524, BL-4525]
touches: [Curl.Conformance.Extended.UnitTests/Coap/UriAndResponse, Documentation/Conformance/Extended/Coap.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4526 — Conformance: CoAP RFC 7252 URI, response code, -f and -i cases and the complete Coap matrix (Coap part 1c)

## Goal

Curl's `coap://` client is measured against RFC 7252 sections 6 and 12 and the response codes, and the Coap requirement matrix is complete for RFC 7252.

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

Scope: section 6 (`coap` URI, Uri-Host, Uri-Port, Uri-Path, Uri-Query composition and decomposition, percent-encoding), section 5.9 response classes as curl reports them (4.xx and 5.xx with and without `-f`, `-i`'s status line) and section 12 (registries the client uses). Last of the parts split from BL-4470; uses BL-4524's server and finishes the matrix.

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Coap.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of RFC 7252 sections 3, 4, 5, 6 and 12 that binds a client a `Coap:` ID mapped to a test or to `n/a: <reason>`, links to `Coap-BlockwiseObserve.md` (BL-4471) and `Coap-Dtls.md` (BL-4472), and the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/Coap/UriAndResponse/` run curl in process and assert datagram bytes, stdout, stderr and exit code for at least: the Uri-* options built from host, port, path segments and query (percent-encoding included, default port omitted), 4.xx and 5.xx with and without `-f`, and `-i`'s status line.
- [ ] Any code this task adds under `Curl.Conformance.Extended.UnitLibrary/Coap/` (in its own new files) is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Coap conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 488e7695 151e64b3 d503fe2c fb1c989b d8e3fb0e ef6c9026.
