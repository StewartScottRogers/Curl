---
id: BL-5625
title: Fix AF-0910: Surviving mutant: an AMQP 1.0 upload whose message fills whole frames can send an extra empty transfer frame
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5625 — Fix AF-0910: Surviving mutant: an AMQP 1.0 upload whose message fills whole frames can send an extra empty transfer frame

## Goal

The defect the audit office reported as AF-0910 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0910 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs:166`

Location: `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs:166`

Invoke-MutationTest -Library Curl.Extended.Protocol.Amqp1.UnitLibrary -MaxMutants 25 -Seed 912464: 'for (var offset = 0; offset < message.Length; offset += perFrame)' mutated to 'offset <= message.Length' survived. When the encoded message is an exact multiple of perFrame, the mutant goes round once more and sends a further transfer frame with an empty payload and more=false after the delivery is complete. That changes the bytes Curl sends to the broker. The only split test (RunAsync_BodyLargerThanPeerFrame_SplitsTransferWithMore) uses 1200 bytes in 512-byte frames, never an exact multiple.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs line 166` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs line 166`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: b49da2cb 9742157d.
