---
id: BL-5365
title: Fix AF-0845: Surviving mutant: a Socket.IO upgrade reply status line of two tokens can be refused with exit 8
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5365 — Fix AF-0845: Surviving mutant: a Socket.IO upgrade reply status line of two tokens can be refused with exit 8

## Goal

The defect the audit office reported as AF-0845 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0845 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:88`

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:88`

statusLine.Length < 2 -> <= 2 survived. A valid status line without a reason phrase, such as 'HTTP/1.1 101', would fail with exit 8 'the WebSocket upgrade reply has no status line' instead of upgrading. No test uses a two-token status line.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs line 88` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs line 88`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: d25acb9e 9742157d.
