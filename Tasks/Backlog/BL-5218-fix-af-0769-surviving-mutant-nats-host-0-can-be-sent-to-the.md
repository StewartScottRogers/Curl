---
id: BL-5218
title: Fix AF-0769: Surviving mutant: nats://host:0/ can be sent to the default port 4222
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5218 — Fix AF-0769: Surviving mutant: nats://host:0/ can be sent to the default port 4222

## Goal

The defect the audit office reported as AF-0769 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0769 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs:57`

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs:57`

Mutant 'url.Port >= 0 ? url.Port : DefaultPort' -> 'url.Port > 0' survived. ExecuteAsync_TheScheme_ChoosesTheDefaultPortAndTls covers no port (4222) and :4443, never an explicit :0. Under the mutant, an explicit port 0 dials 4222 instead of the port the URL names, which changes the connect target.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs line 57` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Nats.UnitLibrary/NatsProtocolHandler.cs line 57`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
