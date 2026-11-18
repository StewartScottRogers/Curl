---
id: BL-5508
title: Fix AF-0866: Surviving mutant: a SignalR URL naming port 0 is sent to the default port
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5508 — Fix AF-0866: Surviving mutant: a SignalR URL naming port 0 is sent to the default port

## Goal

The defect the audit office reported as AF-0866 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0866 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs:60`

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs:60`

Mutant 'int port = url.Port >= 0 ? url.Port : ...' -> 'url.Port > 0' survived Curl.Extended.Protocol.SignalR.UnitTests. A URL that names port 0 would then connect to 80 or 443 instead of the port given, changing the connect target. EndpointUrlOf_MapsTheSchemeToHttpAndKeepsPortPathAndQuery has no port-0 row.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs line 60` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs line 60`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 41f3ae6f 9742157d.
