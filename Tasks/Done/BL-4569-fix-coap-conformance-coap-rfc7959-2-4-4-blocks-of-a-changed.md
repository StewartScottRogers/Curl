---
id: BL-4569
title: Fix Coap conformance: Coap:RFC7959-2.4-4 (blocks of a changed ETag are not joined)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Coap.UnitLibrary/Blockwise/CoapBlockwiseTransfer.cs, Curl.Extended.Protocol.Coap.UnitTests/Blockwise, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Extended.Protocol.Coap.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-02
completed: 2026-11-02
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

- [x] `CoapBlock2Tests.EtagChangesBetweenBlocks_TheMixedBodyIsNotWrittenAndExit56` passes without its
      `KnownFailures.txt` line, and the line `Coap:RFC7959-2.4-4 BL-4563` is removed.
- [x] A focused unit test in `Curl.Extended.Protocol.Coap.UnitTests/Blockwise` pins the ETag check (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green,
      and `Curl.Extended.Protocol.Coap.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10
      (`Measure-CodeQuality.ps1`).

## Notes

- Pipeline `feature` run directly: a one-method fix the Context already designs. `ReceivePayloadAsync` remembers the first response's ETag (or its absence) and throws `CoapException(RecvError)` "CoAP server sent block N of a different representation (its ETag changed)" when a later block's differs; failing rather than restarting from block 0 follows ADR-1057's existing answer to an inconsistent block (no new ADR needed).
- Added `Curl.Extended.Protocol.Coap.UnitLibrary/CLAUDE.md` to touches to keep its Blockwise description true; no task in Doing on origin/work/dark-factory names it.
- Unit tests: `CoapBlockwiseTransferTests.ExecuteAsync_ALaterBlockWithAnotherETag_FailsWithExit56AndWritesNothing` and `ExecuteAsync_EveryBlockWithTheSameETag_IsJoined`. Build clean, fast tests green (Coap 284, Conformance.Extended 474), Measure-CodeQuality for the Coap library: 0 failing members. Platform-neutral (no paths or OS text).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 46047db2 554638a2 6ed7562a.
- 2026-11-02: Doing -> Done. A coap Block2 download whose later block changes ETag fails with exit 56 instead of joining two representations
