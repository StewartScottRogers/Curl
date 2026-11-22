---
id: BL-5735
title: Fix AF-0964: Surviving mutant: a Socket.IO upgrade reply head of exactly 65536 bytes can be refused as too long (WrittenCount > LongestHead -> >=)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5735 — Fix AF-0964: Surviving mutant: a Socket.IO upgrade reply head of exactly 65536 bytes can be refused as too long (WrittenCount > LongestHead -> >=)

## Goal

The defect the audit office reported as AF-0964 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0964 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:66`

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:66`

Seeded sample (seed 51850, -MaxMutants 25): the mutant at SocketIoUpgrade.cs:66 operator '>' survived. Original: 'if (received.WrittenCount > LongestHead)' with LongestHead = 65536 (line 19); mutated: '>='. With the mutant, a reply whose head end has not arrived after exactly 65536 bytes is refused with exit 8 'Socket.IO: the WebSocket upgrade reply head is too long' instead of being read on. The only limit test, ReadReplyAsync_AHeadLongerThanTheLimit_FailsWithExit8, sends 20 x 4000+ bytes, far past the limit, so the boundary is never tested. This changes the exit code.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs line 66` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs line 66`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 7efc6ad1 9742157d.
