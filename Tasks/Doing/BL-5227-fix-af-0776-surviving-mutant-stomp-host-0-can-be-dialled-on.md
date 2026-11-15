---
id: BL-5227
title: Fix AF-0776: Surviving mutant: stomp://host:0/ can be dialled on the default port 61613 instead of port 0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Stomp.UnitLibrary]
requirement: none
created: 2026-11-15
completed:
---
# BL-5227 — Fix AF-0776: Surviving mutant: stomp://host:0/ can be dialled on the default port 61613 instead of port 0

## Goal

The defect the audit office reported as AF-0776 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0776 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:44`

Location: `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:44`

int port = url.Port >= 0 ? url.Port : useTls ? SecureDefaultPort : DefaultPort; mutated to url.Port > 0 survived (seed 68118 sample). With the mutant, an explicit port 0 is replaced by the scheme default, so the transfer connects somewhere the user did not name instead of failing to connect (exit 7). ExecuteAsync_Url_ConnectsToItsPortWithTlsForStomps has no port-0 row. Same pattern as AF-0769 and AF-0774 in NATS and WebTransport.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs line 44` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs line 44`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by ExecuteAsync_Url_ConnectsToItsPortWithTlsForStomps, new rows stomp://broker:0/q and stomps://broker:0/q (port 0). With `> 0` applied by hand both failed; reverted, all 65 pass. Test-only change; build clean.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 89e87e40 9742157d.
