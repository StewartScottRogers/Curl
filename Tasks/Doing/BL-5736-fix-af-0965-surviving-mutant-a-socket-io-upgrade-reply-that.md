---
id: BL-5736
title: Fix AF-0965: Surviving mutant: a Socket.IO upgrade reply that starts with an empty line can be read on instead of failing with exit 8 (IndexOf(HeadEnd) < 0 -> <= 0)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5736 — Fix AF-0965: Surviving mutant: a Socket.IO upgrade reply that starts with an empty line can be read on instead of failing with exit 8 (IndexOf(HeadEnd) < 0 -> <= 0)

## Goal

The defect the audit office reported as AF-0965 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0965 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:64`

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:64`

Seeded sample (seed 51850, -MaxMutants 25): the mutant at SocketIoUpgrade.cs:64 operator '<' survived. Original: 'while ((headEnd = received.WrittenSpan.IndexOf(HeadEnd)) < 0)'; mutated: '<= 0'. A reply beginning with CRLF CRLF has its head end at index 0. The original stops and CheckHead fails it at once with exit 8 'Socket.IO: the WebSocket upgrade reply has no status line' (lines 86-90). The mutant keeps reading until the server closes (exit 52 'Empty reply from server'), 65536 bytes arrive (exit 8, 'too long'), or the transfer times out. No test sends a reply that starts with an empty line. This changes the exit code and can hang.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs line 64` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs line 64`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 38662506 9742157d.
