---
id: BL-4569
title: Fix Coap conformance: Coap:RFC7959-2.4-4 (blocks of a changed ETag are not joined)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Blockwise/CoapBlockwiseTransfer.cs, Curl.Extended.Protocol.Coap.UnitTests/Blockwise, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4569 — Fix Coap conformance: Coap:RFC7959-2.4-4 (blocks of a changed ETag are not joined)

## Goal

A `coap://` Block2 download whose later block carries an ETag different from the first block's is not joined into
one body: curl writes nothing and ends with exit 56 (`CurlExitCode.RecvError`), as it already does for a block out
of order or short.

## Context

Found by BL-4471's conformance case
`Curl.Conformance.Extended.UnitTests/Coap/BlockwiseObserve/CoapBlock2Tests.EtagChangesBetweenBlocks_TheMixedBodyIsNotWrittenAndExit56`
(requirement `Coap:RFC7959-2.4-4` in `Documentation/Conformance/Extended/Coap-BlockwiseObserve.md`): the reference
server answers block 0 with ETag `01 02 03 04` and block 1 with ETag `02`, and curl today joins both and exits 0.
RFC 7959 section 2.4: blocks of different representations (a changed ETag) must not be combined. The fix goes in
`Curl.Extended.Protocol.Coap.UnitLibrary/Blockwise/CoapBlockwiseTransfer.cs` (`ReceivePayloadAsync`/`Append`):
remember the first block's ETag option (or its absence) and throw `CoapException(CurlExitCode.RecvError, ...)`
when a later block's differs. Exit 56 matches the library's existing answer to an inconsistent block (ADR-1057);
restarting from block 0 is RFC-allowed but adds a loop with no bound, so failing is the simpler choice.

## Acceptance criteria

- [ ] `CoapBlock2Tests.EtagChangesBetweenBlocks_TheMixedBodyIsNotWrittenAndExit56` passes without its
      `KnownFailures.txt` line, and the line `Coap:RFC7959-2.4-4 BL-4563` is removed.
- [ ] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests/Blockwise` pins the ETag check (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green,
      and `Curl.Extended.Protocol.Coap.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10
      (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created.
