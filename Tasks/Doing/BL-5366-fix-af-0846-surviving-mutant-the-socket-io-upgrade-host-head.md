---
id: BL-5366
title: Fix AF-0846: Surviving mutant: the Socket.IO upgrade Host header's port boundary is untested
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5366 — Fix AF-0846: Surviving mutant: the Socket.IO upgrade Host header's port boundary is untested

## Goal

The defect the audit office reported as AF-0846 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0846 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:36`

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:36`

url.Port < 0 -> url.Port <= 0 survived. A URL with an explicit port 0 would lose ':0' from the request's Host header (request bytes). Request_AsksForTheWebSocketTransport pins only a URL without a port.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs line 36` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs line 36`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 46ea5fce 9742157d.
