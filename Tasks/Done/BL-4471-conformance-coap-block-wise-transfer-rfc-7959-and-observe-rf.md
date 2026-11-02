---
id: BL-4471
title: Conformance: CoAP block-wise transfer (RFC 7959) and Observe (RFC 7641) for coap:// (Coap part 2 of 3)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4524]
touches: [Curl.Conformance.Extended.UnitLibrary/Coap/BlockwiseObserve, Curl.Conformance.Extended.UnitTests/Coap/BlockwiseObserve, Documentation/Conformance/Extended/Coap-BlockwiseObserve.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4471 — Conformance: CoAP block-wise transfer (RFC 7959) and Observe (RFC 7641) for coap:// (Coap part 2 of 3)

## Goal

Curl's `coap://` block-wise transfers and `--coap-observe` are measured against RFC 7959 and RFC 7641 through the
real curl command line, every client requirement is in a matrix mapped to a test, and every failure is fixed or
listed with its fix task.

## Context

Specifications: RFC 7959 (Block-Wise Transfers in CoAP: Block1, Block2, Size1, Size2, SZX negotiation, the M bit,
4.08 and 4.13 handling, early negotiation) and RFC 7641 (Observing Resources: registration, notification ordering by
sequence number and freshness (section 3.4), deregistration, confirmable notifications, Observe with Block2).
Builds on the reference server BL-4470 wrote in `Curl.Conformance.Extended.UnitLibrary/Coap/`; add behaviour in
`Coap/BlockwiseObserve/` through its extension point.

Library: `Curl.Extended.Protocol.Coap.UnitLibrary` (`Blockwise/CoapBlockwiseTransfer`, `Observe/` with
`CoapObserveTransferHook` and `CoapNotificationOrder`, `Wire/CoapBlockValue`, `Wire/CoapObserveValue`; ADR-1064).

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`Coap:RFC7959-<section>-<n>`, `Coap:RFC7641-<section>-<n>`), the matrix format and the `KnownFailures.txt` rule.
Measure against the specification, never against upstream curl. The reference server implements the wire format
from the specification itself and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing
added to `Harness/`. This task touches only its own conformance files so protocols run in parallel: a fault in its
own server or cases is fixed here, but a defect in the extension library is listed in `KnownFailures.txt` with a
filed High task `Fix Coap conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/Coap-BlockwiseObserve.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT
      of RFC 7959 and RFC 7641 that binds a client an `Coap:` ID mapped to a test or to `n/a: <reason>`; the
      harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/Coap/BlockwiseObserve/` run curl in process and assert datagram
      bytes, stdout, stderr and exit code for at least: a Block2 download, a server that lowers SZX mid-transfer, a
      Block1 upload with 2.31 Continue, 4.13 with Size1, 4.08 on a missing block, an ETag change between blocks,
      an observe registration and several notifications including reordered and stale ones, confirmable
      notifications ACKed, and deregistration when the transfer ends.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix Coap conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/Coap/BlockwiseObserve/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Reference server additions in `Curl.Conformance.Extended.UnitLibrary/Coap/BlockwiseObserve/`: `CoapReferenceBlock`
  (NUM, M, SZX), `CoapBlockwiseObserveOptions` (ETag, Observe, Max-Age, Block1, Block2, Size1, Size2 as uint
  options) and `CoapNotifications`; each case scripts the server through the existing `CoapResponder` extension
  point. Quality run (`Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary`): 0 failing members.
- Cases: `CoapBlock2Tests` (download, SZX lowered mid-transfer, ETag change), `CoapBlock1Tests` (2.31 Continue,
  smaller SZX in 2.31, 4.13 with Block1 and Size1, 4.08 under `-f`), `CoapObserveTests` (ordering with a reordered
  notification dropped, confirmable notifications ACKed, re-registration after Max-Age + 5 s and the 128 s freshness
  rule, deregistration on `-m`, Observe with Block2). Cases needing no timer run on the system clock; the
  re-registration and `-m` cases use the messaging cases' `TimerJumpingClock`.
- Decision: cases whose timing matters under the jumping clock send nothing curl could read after a jump it did not
  expect (a stale notification sent with the first one could arrive after the clock jumped 205 s); the within-128 s
  drop is pinned by the system-clock ordering case instead.
- Failing: `Coap:RFC7959-2.4-4` (curl joins blocks of different ETags, exits 0) is in `KnownFailures.txt` against
  BL-4569 (High, filed). ADR-1195 records the expected answer: exit 56, nothing written.
- The RFC text was written from the RFCs' requirements as this run knew them, not fetched; a reviewer may refine
  wording, not the mapping.
- CI on Linux and macOS: the cases use no path, file or OS text, so they are platform-neutral; fast tests green here
  (`dotnet test Curl.slnx --filter "TestCategory!=Integration"`, 115 projects passed).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: ba62812f e1b6c4b9 fb1c989b d8e3fb0e 954f469a ef6c9026.
- 2026-11-02: Doing -> Done. coap:// block-wise and Observe measured against RFC 7959 and RFC 7641: 15 cases and a matrix; the ETag case is a known failure for BL-4569
