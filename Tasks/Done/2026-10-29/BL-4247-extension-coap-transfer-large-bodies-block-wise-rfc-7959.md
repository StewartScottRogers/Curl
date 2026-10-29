---
id: BL-4247
title: Extension CoAP: transfer large bodies block-wise (RFC 7959)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4246]
touches: [Curl.Extension.Coap.UnitLibrary/Blockwise, Curl.Extension.Coap.UnitTests/Blockwise, Curl.Extension.Coap.UnitLibrary/Client, Curl.Extension.Coap.UnitTests/Client, Curl.Extension.Coap.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4247 — Extension CoAP: transfer large bodies block-wise (RFC 7959)

## Goal

A `coap://` download larger than one datagram arrives whole through Block2, and an upload (`-d`, `-T`) larger than one block goes out through Block1, by RFC 7959.

## Context

Part of BL-4116 (extension 15 of 63, split for size). Extension rules: ADR-0999 and the lane rules "Extensions": it references only `Curl.Protocol.Abstractions`, the shared layers (`Curl.Tls`, `Curl.Cryptography`, `Curl.Networking`) and `Curl.Extension.Abstractions`, never a `Curl.Protocol.*` library or another extension, is tested against the RFCs, not curl, and never changes what curl does. Held to the four quality gates.

Builds on BL-4246's client and BL-4245's block option codec. RFC 7959 sections 2.3 (Block options in requests and responses), 2.4 (Block2), 2.5 (Block1), 2.9 (2.31 Continue, 4.08, 4.13), 3 (examples). Default block size 1024 (SZX 6), following a smaller size the server asks for. Namespace `Curl.Extension.Coap.Blockwise`.

## Acceptance criteria

- [x] Unit tests over a fake `IDatagramConnector` replay the exchanges in RFC 7959 section 3's figures for Block2 (including the server choosing a smaller size) and Block1 (including 2.31 Continue and 4.13 Request Entity Too Large), each named after its figure.
- [x] A body of several blocks is written to the output in order and only once.
- [x] The changed library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Pipeline `feature` run in the session without sub-agents: the change is one new class plus two small
  handler edits in one library, so a planner and implementer would cost more than they save.
- Design (ADR-1057): `Blockwise/CoapBlockwiseTransfer` wraps each `CoapExchange`. Block1 at 1024 bytes
  (SZX 6), following a smaller size a 2.31 asks for, and restarting from block 0 at the smaller size a 4.13
  asks for; Block2 at the server's size, by byte offset; the body is joined in memory and written once.
  `-T` now gives a PUT whose payload is the whole upload (`CoapRequest.PayloadOfAsync`).
- Tests (`Curl.Extension.Coap.UnitTests/Blockwise/CoapBlockwiseTransferTests.cs`): Figures 2, 7, 8 and 10;
  the server shrinking its size mid-transfer (section 2.4); 4.13 with and without a smaller Block1
  (section 2.9.3, which has no figure in section 3, so the test is named after the code); 4.08; a
  repeated block written once; out of order, short and SZX 7 blocks (exit 56). The client starts at
  1024-byte blocks, so a figure's 128-byte blocks are the server's choice or the client's first block is
  1024 bytes.
- Fixed `ScriptedDatagramChannel` (test fake): it handed a reply to a receive the previous exchange had
  cancelled, losing it, so a second exchange on one channel hung.
- Added `Curl.Extension.Coap.UnitLibrary/CLAUDE.md` to `touches` to describe `Blockwise/`; no task in
  Doing on `origin/work/dark-factory` names it.
- Measure-CodeQuality.ps1 -Library Curl.Extension.Coap.UnitLibrary: 100% line, 100% branch, 0 failing
  members, worst CRAP 10. Solution build clean; fast tests green on Windows. All tests use only
  in-memory fakes, no paths or OS text, so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 4d9c8de0 d39fb701 54cafd17 3d836d5e.
- 2026-10-29: Doing -> Done. coap:// downloads arrive whole through Block2 and -d/-T uploads go out through Block1 (RFC 7959)
